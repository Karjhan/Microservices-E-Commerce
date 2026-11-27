# Catalog Service

The Catalog service owns the product catalog of the 3D-printing store. It is an ASP.NET Core (.NET 10) minimal-API
service that stores products (including 3D-printing metadata such as supported materials, compatible printer
technologies, print settings and physical dimensions), their images, free-form attributes and product-to-product
relations. It publishes a domain event to RabbitMQ after every successful write so other services (for example the
GraphRAG ingestion worker) can keep their own read models in sync.

> This document describes what the code in `Catalog/` actually does. Known gaps and surprising behaviours are
> collected in [Known gaps and inconsistencies](#known-gaps-and-inconsistencies).

## Table of contents

1. [Role in the system](#role-in-the-system)
2. [Tech stack](#tech-stack)
3. [Solution layout](#solution-layout)
4. [HTTP API](#http-api)
5. [Messaging (RabbitMQ)](#messaging-rabbitmq)
6. [Data storage](#data-storage)
7. [Domain model and business rules](#domain-model-and-business-rules)
8. [Configuration](#configuration)
9. [Observability](#observability)
10. [Running locally](#running-locally)
11. [Tests](#tests)
12. [Security notes](#security-notes)
13. [Known gaps and inconsistencies](#known-gaps-and-inconsistencies)

---

## Role in the system

| Aspect | Details |
|---|---|
| Service name | `catalog-service` (`ServiceName` setting, used as the OpenTelemetry service name); docker-compose service/container name: `catalog` |
| Responsibility | CRUD for products, product images (object storage), product attributes, product relations; product listing with filtering and pagination |
| Datastores | PostgreSQL (database `catalog`), Redis (read cache), MinIO (bucket `products`) |
| Messaging | Publishes on RabbitMQ topic exchange `catalog-exchange`. Consumes one event: `inventory.updated` from `inventory-exchange` (queue `catalog.inventory.updated`), which moves a product between `Active` and `OutOfStock` (see Topology) |
| HTTP callers | Reached through the API gateway (the repo's gateway proxies `/products/**` to `http://catalog:8080`) or directly during development |
| HTTP dependencies | None (it does not call other services over HTTP) |
| Event consumers | The GraphRAG service is configured (in `docker/docker-compose.yml`) with `RABBITMQ_EXCHANGE=catalog-exchange` and queue `graphrag-ingestion`; its consumer binds the product, relation and attribute routing keys listed below |

---

## Tech stack

| Area | Technology |
|---|---|
| Language / runtime | C# with nullable reference types and implicit usings, `net10.0` (all projects) |
| Web framework | ASP.NET Core minimal APIs (`Microsoft.NET.Sdk.Web`), endpoint groups via `MapGroup("/products")` |
| Mediator / CQRS | `MediatR` 13.0.0 (one command/query + handler per use case; `LoggingBehavior` pipeline behavior) |
| ORM / database | Entity Framework Core 10.0.6, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1 (PostgreSQL) |
| Cache | `StackExchange.Redis` 2.12.14 |
| Messaging | `RabbitMQ.Client` 7.2.1 (async API) |
| Object storage | `Minio` 7.0.0 (.NET SDK) |
| Health checks | `AspNetCore.HealthChecks.NpgSql` / `.Rabbitmq` / `.Redis` / `.UI.Client` 9.0.0, `Microsoft.Extensions.Diagnostics.HealthChecks` 10.0.6 |
| Observability | OpenTelemetry 1.15.x: OTLP exporter, ASP.NET Core / HttpClient / EF Core / StackExchange.Redis instrumentation |
| Misc | `Microsoft.AspNetCore.OpenApi` 10.0.3 is referenced, but no OpenAPI/Swagger endpoint is mapped |
| Unit tests | xUnit 2.9.3, FluentAssertions 8.9.0, Moq 4.20.72, EF Core InMemory and SQLite providers |
| Integration tests | xUnit, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers 4.11.0 (PostgreSql, Redis, RabbitMq, Minio), Respawn 7.0.0 (referenced) |
| Container | Multi-stage `Dockerfile` on `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0`, listens on 8080 |

---

## Solution layout

`Catalog.sln` contains seven projects:

```
Catalog/
  Catalog.sln
  .dockerignore
  Catalog/                  ASP.NET Core host (presentation layer)
    Program.cs              Composition root, middleware, health checks, route groups, dev-time migrate + bucket creation
    Endpoints/Products/     One static class per endpoint (MapXxx extension methods) + request records
    Endpoints/Health/       MinioHealthCheck (used) and HealthCheckEndpoint.MapHealth (defined but not called)
    Middleware/             CorrelationIdMiddleware, RequestLoggingMiddleware, ExceptionMiddleware
    appsettings.json        Defaults using docker-network host names
    appsettings.Development.json   Defaults using localhost
    Properties/launchSettings.json
    Catalog.http            Sample requests (VS Code / Rider HTTP client)
    Dockerfile
  Application/              Use cases (MediatR commands/queries/handlers), DTOs, cache-key helper
    Features/Products/<UseCase>/   <UseCase>Command|Query.cs + <UseCase>Handler.cs
    DTOs/                   ProductDto, PaginatedListDto<T>, AttributeDto, RelationDto, ProductImageDto, ...
    Common/LoggingBehavior.cs      MediatR pipeline behavior (Result.cs and Errors.cs are empty placeholders)
    CacheKeyGenerator.cs
  Domain/                   Entities, value objects, enums (no dependencies)
    Entities/               Product (aggregate root), ProductImage, ProductAttribute, ProductRelation
    Commons/                PrintSettings, Dimensions, ProductFilter (ProductFile is defined but not mapped or used)
    Enums/                  ProductStatus, MaterialType, PrinterType
  Infrastructure/           EF Core, Redis, RabbitMQ, MinIO, OpenTelemetry, options classes
    Abstractions/           IProductRepository, ICacheService, IEventPublisher, IFileStorageService
    Persistence/            CatalogDbContext, Configurations/, Migrations/, Repositories/ProductRepository
    Caching/RedisCacheService, Messaging/RabbitMqPublisher, Storage/MinioFileStorageService
    Observability/OpenTelemetrySetup, Configuration/*Options, DependencyInjection/
  Contracts/                Event payload classes (Contracts.Events) and ActivitySources
  UnitTests/                Handler, repository, Redis and MinIO unit tests
  IntegrationTests/         HTTP-level tests against real containers (Testcontainers)
```

Project references: `Catalog` -> `Application` -> (`Infrastructure`, `Contracts`); `Infrastructure` -> (`Domain`, `Contracts`).
Note that the dependency direction is not a strict clean-architecture one: `Application` references `Infrastructure`,
and the ports `IEventPublisher` / `IFileStorageService` live in the `Infrastructure` project (namespace
`Application.Abstractions.*`), while `ICacheService` and `IProductRepository` use the `Infrastructure.Abstractions.*` namespaces.

### Request flow

```
HTTP request
  -> CorrelationIdMiddleware -> RequestLoggingMiddleware -> ExceptionMiddleware
  -> minimal-API endpoint -> IMediator.Send(command/query)
  -> LoggingBehavior -> Handler
       -> IProductRepository (EF Core / PostgreSQL)
       -> ICacheService (Redis)            read-through caching and invalidation
       -> IFileStorageService (MinIO)      images
       -> IEventPublisher (RabbitMQ)       event after the DB commit
```

---

## HTTP API

Base path: `/products`. Host port depends on how the service is started (see [Running locally](#running-locally)).
JSON is `camelCase` in requests and responses (ASP.NET Core defaults). Enums in request bodies are accepted as
strings (`"PLA"`; `JsonStringEnumConverter` is registered, integers are also accepted by that converter) and are
returned as strings. Enum query parameters are bound by name (`material=PLA`).

**Authentication / authorization: none.** No authentication or authorization middleware is registered and no endpoint
declares a role or policy. All endpoints are anonymous at the service level; access control must be enforced upstream.

### Endpoint summary

| Method | Path | Auth | Success | Description |
|---|---|---|---|---|
| POST | `/products` | none | `201` | Create a product (status `Draft`) |
| GET | `/products` | none | `200` | List/filter products (paginated, cached 5 min) |
| GET | `/products/{id}` | none | `200` | Get one product (cached 10 min) |
| PUT | `/products/{id}` | none | `204` | Replace product details |
| DELETE | `/products/{id}` | none | `204` | Delete product, its images (MinIO), attributes, outgoing and incoming relations |
| POST | `/products/{id}/images` | none | `200` | Upload an image (multipart/form-data, max 5 MB) |
| DELETE | `/products/{productId}/images/{imageId}` | none | `204` | Delete an image (MinIO object and DB row) |
| POST | `/products/{productId}/attributes` | none | `201` | Add a key/value attribute |
| GET | `/products/{productId}/attributes` | none | `200` | List attributes (ordered by key) |
| DELETE | `/products/{productId}/attributes/{attributeId}` | none | `204` | Delete an attribute |
| POST | `/products/{productId}/relations` | none | `201` | Add a relation to another product |
| GET | `/products/{productId}/relations` | none | `200` | List relations (ordered by type, then related id) |
| DELETE | `/products/{productId}/relations/{relationId}` | none | `204` | Delete a relation |
| GET | `/health` | none | `200` / `503` | Dependency health report (not under `/products`; not mapped in the `Testing` environment) |

All `{id}`, `{productId}`, `{attributeId}`, `{relationId}`, `{imageId}` route values are constrained to GUIDs
(`:guid`); a non-GUID value does not match the route (404).

### Error responses

`ExceptionMiddleware` converts exceptions into a JSON body `{ "error": "<message>", "status": <code> }`:

| Exception | Status | Typical cause |
|---|---|---|
| `KeyNotFoundException` | `404` | Product, attribute, relation, image or related product not found |
| `ArgumentException` | `400` | Empty attribute key/value, empty relation type, relation to itself |
| any other exception | `500` | Includes duplicate attribute key / duplicate relation (`InvalidOperationException`), unique-constraint violations (for example duplicate product slug), missing/invalid request body members, broker/storage failures |

```json
{ "error": "Product not found", "status": 404 }
```

Every exception is logged at error level. Responses never use `409` or `422`; there is no request validation layer
(no FluentValidation or data annotations).

### POST /products

Creates a product. The new product always starts in status `Draft` and its slug is derived from the name.

Request body (`CreateProductRequest`):

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | yes | max 200 (DB) |
| `shortDescription` | string | yes | max 500 (DB) |
| `longDescription` | string | yes | text |
| `price` | decimal | yes | stored as `numeric(18,2)`; no range validation |
| `categoryId` | GUID | yes | no existence check (there is no category in this service) |
| `currency` | string | yes in practice | max 10 (DB). Sent value is always applied; omitting it fails on save (the entity default `EUR` is overwritten with `null`) |
| `settings` | object | yes in practice | `PrintSettings`; send `{}` at minimum. See the binding note below |
| `size` | object | yes in practice | `Dimensions`; send `{}` at minimum. See the binding note below |
| `tags` | string[] | no | trimmed, blank entries dropped, de-duplicated |
| `supportedMaterials` | string[] | no | any of `PLA`, `ABS`, `PETG`, `Resin`, `Nylon`, `TPU`; de-duplicated |
| `compatiblePrinters` | string[] | no | any of `FDM`, `SLA`, `SLS`; de-duplicated |

`PrintSettings` fields: `layerHeight` (decimal?), `infillPercentage` (int?), `nozzleSize` (int?),
`printTimeMinutes` (int?), `filamentUsedGrams` (decimal?), `supportsRequired` (bool).
`Dimensions` fields: `widthMm`, `heightMm`, `depthMm` (decimal).

> Binding note: `PrintSettings` and `Dimensions` declare their properties with `private set` and no
> `[JsonInclude]`. `System.Text.Json` does not populate such properties, so values sent under `settings` and `size`
> are silently ignored today and the stored values are always the defaults (nulls / `0` / `false`). The objects must
> still be present. See [Known gaps](#known-gaps-and-inconsistencies).

```json
{
  "name": "3D Printed Vase",
  "shortDescription": "Modern decorative vase",
  "longDescription": "A high-quality 3D printed vase suitable for modern interiors.",
  "price": 25.50,
  "categoryId": "11111111-1111-1111-1111-111111111111",
  "currency": "EUR",
  "settings": { "layerHeight": 0.2, "infillPercentage": 20, "supportsRequired": false },
  "size": { "widthMm": 100, "heightMm": 150, "depthMm": 100 },
  "tags": ["vase", "decor", "modern"],
  "supportedMaterials": ["PLA", "PETG"],
  "compatiblePrinters": ["FDM"]
}
```

Response `201 Created`, header `Location: /products/{productId}`:

```json
{ "productId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100" }
```

Side effects: removes all `products:*` cache keys; publishes `product.created`.

### GET /products

Query parameters (all optional; filters are AND-combined):

| Parameter | Type | Semantics |
|---|---|---|
| `categoryId` | GUID | exact match |
| `minPrice`, `maxPrice` | decimal | inclusive bounds on `price` |
| `tags` | string, repeatable (`tags=a&tags=b`) | product matches if it has **any** of the given tags (exact, case-sensitive) |
| `material` | `MaterialType` | product's `supportedMaterials` contains the value |
| `printerType` | `PrinterType` | product's `compatiblePrinters` contains the value |
| `search` | string | case-insensitive substring (`ILIKE '%term%'`) on `name` or `shortDescription` |
| `status` | `ProductStatus` (`Draft`, `Active`, `Inactive`, `OutOfStock`, `Discontinued`) | exact match; **not applied by default** (all statuses are returned unless given) |
| `page` | int | 1-based; a value of `0` is treated as `1` |
| `pageSize` | int | a value of `0` is treated as `20`; there is no upper bound |

`page` and `pageSize` are declared as non-nullable `int` in the endpoint signature, so ASP.NET Core minimal-API
binding treats them as required query parameters; the `0` fallbacks only apply when `0` is sent explicitly. Always send both.
Results are ordered by `createdAt` descending.

Response `200 OK` (`PaginatedListDto<ProductDto>`; `totalPages = ceil(totalCount / pageSize)`):

```json
{
  "items": [
    {
      "id": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
      "name": "3D Printed Vase",
      "slug": "3d-printed-vase",
      "shortDescription": "Modern decorative vase",
      "longDescription": "A high-quality 3D printed vase suitable for modern interiors.",
      "price": 25.50,
      "currency": "EUR",
      "status": "Draft",
      "categoryId": "11111111-1111-1111-1111-111111111111",
      "settings": {
        "layerHeight": null,
        "infillPercentage": null,
        "nozzleSize": null,
        "printTimeMinutes": null,
        "filamentUsedGrams": null,
        "supportsRequired": false
      },
      "size": { "widthMm": 0, "heightMm": 0, "depthMm": 0 },
      "tags": ["vase", "decor", "modern"],
      "attributes": [
        { "attributeId": "834ec5f2-6c9c-4db7-8a8e-2c6109490fc0", "key": "Color", "value": "White" }
      ],
      "relatedProducts": [
        { "relationId": "40d7aea4-0427-4624-a677-457913b1a241", "relatedProductId": "66959dea-307e-4f09-8d9c-8d4c352f038a", "type": "similar" }
      ],
      "supportedMaterials": ["PLA", "PETG"],
      "compatiblePrinters": ["FDM"],
      "images": [
        {
          "imageId": "0b9a8f43-6a34-4d0e-9a47-5f2f6c5e1a10",
          "objectKey": "products/7de9eda6-2c8c-4bb8-bf26-2a7346865100/5c1c1d5e-0f0a-4f6b-8d9a-0f5e1d6a9b11-vase.jpg",
          "url": "http://localhost:9000/products/products/7de9eda6-2c8c-4bb8-bf26-2a7346865100/5c1c1d5e-0f0a-4f6b-8d9a-0f5e1d6a9b11-vase.jpg",
          "isPrimary": true
        }
      ],
      "downloadCount": 0,
      "averageRating": 0,
      "createdAt": "2026-05-01T18:00:00Z",
      "updatedAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Caching: the whole page is cached under `products:<sha256(filter)>` for 5 minutes (see [Redis](#redis-cache)).
Filtering for tags, material and printer type, as well as pagination and counting, is done in memory after loading
all products matching the SQL filters (see [Known gaps](#known-gaps-and-inconsistencies)).

### GET /products/{id}

Returns a single `ProductDto` (same shape as an item above). `404` if the product does not exist. Cached under
`product:{id}` for 10 minutes.

### PUT /products/{id}

Replaces the mutable fields of a product. Status, category and images cannot be changed here.

| Field | Type | Required | Notes |
|---|---|---|---|
| `name`, `shortDescription`, `longDescription`, `price` | as in create | yes | `name` change regenerates the slug |
| `currency` | string | yes in practice | |
| `settings`, `size` | objects | yes in practice | same binding caveat as create |
| `tags`, `supportedMaterials`, `compatiblePrinters` | arrays | no | **omitted/`null` = keep current**; an empty array clears the list |
| `downloadCount` | int | no, default `0` | the request record defaults it to `0`, so omitting it **resets** the stored value to `0` |
| `averageRating` | double | no, default `0` | same: omitting it resets the stored value to `0` |

```json
{
  "name": "3D Printed Vase PRO",
  "shortDescription": "Updated version",
  "longDescription": "Improved durability and finish.",
  "price": 30.00,
  "currency": "EUR",
  "settings": {},
  "size": {},
  "tags": ["vase", "premium"],
  "supportedMaterials": ["PLA", "ABS"],
  "compatiblePrinters": ["FDM", "SLA"],
  "downloadCount": 12,
  "averageRating": 4.5
}
```

Response `204 No Content`; `404` if not found. Side effects: removes `product:{id}` and all `products:*` keys;
publishes `product.updated`.

### DELETE /products/{id}

Response `204`; `404` if not found. Behaviour (in order): deletes every image object from MinIO; removes all relations
in other products that point at this product (they have no FK, so this is done explicitly); deletes the product row
(attributes, images and outgoing relations cascade); saves; then publishes, for each of the product's attributes
`product.attribute.deleted`, for each outgoing relation and each removed incoming relation `product.relation.deleted`,
and finally `product.deleted`; invalidates `product:{id}` and `products:*`.

### POST /products/{id}/images

`multipart/form-data` with two parts:

| Part | Type | Notes |
|---|---|---|
| `file` | file | required; larger than 5 MiB returns `400` with body `"File too large"`; content type is not validated |
| `isPrimary` | bool form field | required; when `true`, all existing images of the product are set to non-primary first |

The upload is streamed to a temporary file, stored in MinIO, then the temp file is deleted. Antiforgery is disabled
for this endpoint.

Response `200 OK`:

```json
{
  "imageId": "0b9a8f43-6a34-4d0e-9a47-5f2f6c5e1a10",
  "url": "http://localhost:9000/products/products/7de9eda6-2c8c-4bb8-bf26-2a7346865100/5c1c1d5e-0f0a-4f6b-8d9a-0f5e1d6a9b11-vase.jpg"
}
```

`404` if the product does not exist. Publishes `product.image.uploaded`. This operation does **not** invalidate the
product/list cache.

### DELETE /products/{productId}/images/{imageId}

Deletes the MinIO object, removes the image row, publishes `product.image.deleted`; response `204`. `404` if product or
image (for that product) is missing. Does not invalidate the cache and does not promote another image to primary.

### POST /products/{productId}/attributes

Request (`AddProductAttributeRequest`):

```json
{ "key": "Color", "value": "White" }
```

Rules: `key` and `value` must be non-blank (`400` otherwise); key and value are trimmed before storing; adding a key
that already exists on the product fails (generic `500`, see [Error responses](#error-responses)); DB limits are
100 characters for the key and 500 for the value.

Response `201 Created`, `Location: /products/{productId}/attributes/{attributeId}` (there is no GET for a single attribute):

```json
{ "attributeId": "834ec5f2-6c9c-4db7-8a8e-2c6109490fc0" }
```

Side effects: removes `product:{id}` and `products:*`; publishes `product.attribute.added`.

### GET /products/{productId}/attributes

Response `200 OK`, ordered by key. An unknown product id returns an empty list (no existence check):

```json
[ { "attributeId": "834ec5f2-6c9c-4db7-8a8e-2c6109490fc0", "key": "Color", "value": "White" } ]
```

### DELETE /products/{productId}/attributes/{attributeId}

`204`; `404` if the product or the attribute (for that product) is missing. Invalidates caches; publishes
`product.attribute.deleted`.

### POST /products/{productId}/relations

Request (`AddProductRelationRequest`); `type` is a free-form label (for example `similar`), trimmed, max 50 characters (DB):

```json
{ "relatedProductId": "66959dea-307e-4f09-8d9c-8d4c352f038a", "type": "similar" }
```

Rules: a product cannot relate to itself (`400`); `type` must be non-blank (`400`); both products must exist (`404`);
a product can have at most one relation to a given related product regardless of type (otherwise generic `500`).
Relations are directional (no reverse relation is created).

Response `201 Created`, `Location: /products/{productId}/relations/{relationId}`:

```json
{ "relationId": "40d7aea4-0427-4624-a677-457913b1a241" }
```

Side effects: invalidates caches (for the source product only); publishes `product.relation.added`.

### GET /products/{productId}/relations

Response `200 OK`, ordered by type then related product id. An unknown product id returns an empty list:

```json
[ { "relationId": "40d7aea4-0427-4624-a677-457913b1a241", "relatedProductId": "66959dea-307e-4f09-8d9c-8d4c352f038a", "type": "similar" } ]
```

### DELETE /products/{productId}/relations/{relationId}

`204`; `404` if the product or the relation (for that product) is missing. Invalidates caches; publishes
`product.relation.deleted`.

### GET /health

JSON report with one entry per check (`postgres`, `redis` if configured, `rabbitmq`, `minio`). The MinIO check verifies
that the configured bucket exists. The response status code is the default ASP.NET Core health-check mapping
(`200` for Healthy/Degraded, `503` for Unhealthy).

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgres", "status": "Healthy", "error": null },
    { "name": "redis", "status": "Healthy", "error": null },
    { "name": "rabbitmq", "status": "Healthy", "error": null },
    { "name": "minio", "status": "Healthy", "error": null }
  ]
}
```

---

## Messaging (RabbitMQ)

### Topology

| Item | Value |
|---|---|
| Role | Publisher, plus one consumer (below) |
| Exchange | `catalog-exchange` |
| Exchange type | `topic` |
| Durable | `true` (auto-delete not set, so `false`) |
| Declared by | `RabbitMqPublisher`, idempotently before every publish |
| Connection | single shared `IConnection` created from `RabbitMQ` settings (host, port, user, password, vhost) on first use, no retry |
| Channel | a new channel per published message, disposed after the publish |
| Publish flags | `mandatory: false`; no publisher confirms; no message properties are set (no explicit persistent delivery mode, no content-type, no message id/correlation id headers) |
| Serialization | UTF-8 JSON via `System.Text.Json` with **default options**, so property names are **PascalCase** exactly as in the contract classes, enums are sent as strings (already strings in the contracts), GUIDs as strings, `DateTime` as ISO-8601 UTC |

Events are published from the MediatR handlers **after** the database commit and after cache invalidation. There is no
outbox: if the broker is unavailable the HTTP call fails with `500` after the data has already been committed, and
failed publishes are not retried. Delivery is therefore at-most-once. Queues, retry and dead-letter policy are the
responsibility of each consumer (for example `graphrag-ingestion` in the GraphRAG service).

### Consumed events

| Exchange | Routing key | Queue | Consumer |
|---|---|---|---|
| `inventory-exchange` | `inventory.updated` | `catalog.inventory.updated` (durable) | `InventoryUpdatedConsumer` |

The payload carries `ProductId` and `AvailableQuantity`. An `Active` product with nothing available becomes `OutOfStock`; an `OutOfStock` product with stock again becomes `Active`. `Draft`, `Inactive` and `Discontinued` products are never touched, because an administrator chose those. A change saves the product, removes `product:{id}` and `products:*` from the cache and publishes `product.status.changed`. Messages are acknowledged after handling and requeued on failure; events for unknown products are acknowledged and ignored.

### Published events

| Routing key | Payload class (`Contracts.Events`) | Published by |
|---|---|---|
| `product.created` | `ProductCreated` | `POST /products` |
| `product.updated` | `ProductUpdated` | `PUT /products/{id}` |
| `product.deleted` | `ProductDeleted` | `DELETE /products/{id}` |
| `product.image.uploaded` | `ProductImageUploaded` | `POST /products/{id}/images` |
| `product.image.deleted` | `ProductImageDeleted` | `DELETE /products/{productId}/images/{imageId}` |
| `product.attribute.added` | `ProductAttributeAdded` | `POST .../attributes` |
| `product.attribute.deleted` | `ProductAttributeDeleted` | `DELETE .../attributes/{id}`; also once per attribute when a product is deleted |
| `product.relation.added` | `ProductRelationAdded` | `POST .../relations` |
| `product.relation.deleted` | `ProductRelationDeleted` | `DELETE .../relations/{id}`; also once per outgoing relation and once per removed incoming relation when a product is deleted |

Event order on `DELETE /products/{id}`: all `product.attribute.deleted`, all outgoing `product.relation.deleted`,
all incoming `product.relation.deleted`, then `product.deleted`.

Events carry no envelope or version: the routing key is the only discriminator. Consumers should be idempotent
(there is no event id; use the aggregate ids in the payload).

### Event payloads

#### `product.created` - `ProductCreated`

| Field | Type |
|---|---|
| `ProductId` | GUID |
| `Name`, `Slug`, `ShortDescription`, `LongDescription` | string |
| `Price` | decimal |
| `Currency` | string |
| `CategoryId` | GUID |
| `Status` | string (always `"Draft"` for a new product) |
| `Tags` | string[] |
| `DownloadCount` | int (not populated by the handler, so always `0`) |
| `AverageRating` | double (not populated, always `0`) |
| `Attributes` | `{ Key, Value }[]` (always empty for a new product) |
| `SupportedMaterials`, `CompatiblePrinters` | string[] (enum names) |
| `CreatedAt` | DateTime (UTC) |

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "Name": "3D Printed Vase",
  "Slug": "3d-printed-vase",
  "ShortDescription": "Modern decorative vase",
  "LongDescription": "A high-quality 3D printed vase suitable for modern interiors.",
  "Price": 25.50,
  "Currency": "EUR",
  "CategoryId": "11111111-1111-1111-1111-111111111111",
  "Status": "Draft",
  "Tags": ["vase", "decor", "modern"],
  "DownloadCount": 0,
  "AverageRating": 0,
  "Attributes": [],
  "SupportedMaterials": ["PLA", "PETG"],
  "CompatiblePrinters": ["FDM"],
  "CreatedAt": "2026-05-01T18:00:00.123456Z"
}
```

#### `product.updated` - `ProductUpdated`

| Field | Type |
|---|---|
| `ProductId` | GUID |
| `Name`, `Slug`, `ShortDescription`, `LongDescription` | string |
| `Price` | decimal |
| `Currency` | string |
| `Status` | string |
| `DownloadCount` | int |
| `AverageRating` | double (the handler casts the value to `int` first, so the fractional part is lost: `4.5` is published as `4`) |
| `Tags`, `SupportedMaterials`, `CompatiblePrinters` | string[] |
| `UpdatedAt` | DateTime (UTC) |

`CategoryId` is not part of this event.

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "Name": "3D Printed Vase PRO",
  "Slug": "3d-printed-vase-pro",
  "ShortDescription": "Updated version",
  "LongDescription": "Improved durability and finish.",
  "Price": 30.00,
  "Currency": "EUR",
  "Status": "Draft",
  "DownloadCount": 12,
  "AverageRating": 4,
  "Tags": ["vase", "premium"],
  "SupportedMaterials": ["PLA", "ABS"],
  "CompatiblePrinters": ["FDM", "SLA"],
  "UpdatedAt": "2026-05-02T09:30:00.123456Z"
}
```

#### `product.deleted` - `ProductDeleted`

```json
{ "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100" }
```

#### `product.image.uploaded` - `ProductImageUploaded`

Fields: `ProductId` (GUID), `ImageId` (GUID), `ImageUrl` (string), `ObjectKey` (string), `IsPrimary` (bool).
The handler only sets `ProductId`, `ImageUrl` and `ObjectKey`, so `ImageId` is always the empty GUID and `IsPrimary`
is always `false` in the published message.

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "ImageId": "00000000-0000-0000-0000-000000000000",
  "ImageUrl": "http://localhost:9000/products/products/7de9eda6-2c8c-4bb8-bf26-2a7346865100/5c1c1d5e-0f0a-4f6b-8d9a-0f5e1d6a9b11-vase.jpg",
  "ObjectKey": "products/7de9eda6-2c8c-4bb8-bf26-2a7346865100/5c1c1d5e-0f0a-4f6b-8d9a-0f5e1d6a9b11-vase.jpg",
  "IsPrimary": false
}
```

#### `product.image.deleted` - `ProductImageDeleted`

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "ImageId": "0b9a8f43-6a34-4d0e-9a47-5f2f6c5e1a10",
  "ObjectKey": "products/7de9eda6-2c8c-4bb8-bf26-2a7346865100/5c1c1d5e-0f0a-4f6b-8d9a-0f5e1d6a9b11-vase.jpg"
}
```

#### `product.attribute.added` - `ProductAttributeAdded`

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "AttributeId": "834ec5f2-6c9c-4db7-8a8e-2c6109490fc0",
  "Key": "Color",
  "Value": "White"
}
```

#### `product.attribute.deleted` - `ProductAttributeDeleted`

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "AttributeId": "834ec5f2-6c9c-4db7-8a8e-2c6109490fc0",
  "Key": "Color"
}
```

#### `product.relation.added` - `ProductRelationAdded`

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "RelationId": "40d7aea4-0427-4624-a677-457913b1a241",
  "RelatedProductId": "66959dea-307e-4f09-8d9c-8d4c352f038a",
  "Type": "similar"
}
```

#### `product.relation.deleted` - `ProductRelationDeleted`

For an incoming relation removed because the *target* product was deleted, `ProductId` is the product that owned the
relation and `RelatedProductId` is the deleted product.

```json
{
  "ProductId": "7de9eda6-2c8c-4bb8-bf26-2a7346865100",
  "RelationId": "40d7aea4-0427-4624-a677-457913b1a241",
  "RelatedProductId": "66959dea-307e-4f09-8d9c-8d4c352f038a",
  "Type": "similar"
}
```

### Consumer behaviour

Catalog has no consumers, so there is no idempotency store, retry, or dead-letter handling in this service.

---

## Data storage

### PostgreSQL

* Database name: `catalog` (from the connection string). Provider: Npgsql (EF Core 10). Schema: default (`public`).
* Migrations live in `Infrastructure/Persistence/Migrations` and are applied automatically at startup **only when
  `ASPNETCORE_ENVIRONMENT=Development`** (`db.Database.Migrate()`, which also creates the database if missing). In any
  other environment migrations are not applied by the service.
* Migration history: `InitialCreate` (2026-04-28), `FixProductImages`, `FixProduct`, `FixProductImage`,
  `FixProduct3DStructure` (moves print settings/dimensions into owned columns and creates `ProductAttributes` and
  `ProductRelations`), and two empty follow-ups (`FixProduct3DStructure2`, `FixProduct3DStructure3`).
* Table and column names are EF defaults (PascalCase, quoted identifiers in SQL).
* Entities are tracked through `CatalogDbContext` (`Products`, `ProductImages`, `ProductAttributes`, `ProductRelations`).
  `ProductFile` is not mapped.

#### Table `Products`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK; generated in the entity constructor (`Guid.NewGuid()`) |
| `Name` | `varchar(200)` | no | |
| `Slug` | `varchar(200)` | no | unique index `IX_Products_Slug` |
| `ShortDescription` | `varchar(500)` | no | |
| `LongDescription` | `text` | no | |
| `Price` | `numeric(18,2)` | no | |
| `Currency` | `varchar(10)` | no | |
| `Status` | `integer` | no | `ProductStatus` stored as its **integer** value (`0` Draft, `1` Active, `2` Inactive, `3` OutOfStock, `4` Discontinued); index `IX_Products_Status` |
| `CategoryId` | `uuid` | no | index `IX_Products_CategoryId`; no FK (no category table) |
| `Tags` | `text` | no | comma-separated string, e.g. `vase,decor,modern`; empty list stored as empty string. Tags containing a comma would be split on read |
| `SupportedMaterials` | `text` | no | comma-separated enum **names**, e.g. `PLA,PETG` (parsed back with `Enum.Parse`) |
| `CompatiblePrinters` | `text` | no | comma-separated enum **names**, e.g. `FDM,SLA` |
| `DownloadCount` | `integer` | no | default `0` |
| `AverageRating` | `double precision` | no | (`HasPrecision(5,2)` is configured but has no effect on a `double` column) |
| `CreatedAt` | `timestamptz` | no | UTC |
| `UpdatedAt` | `timestamptz` | yes | UTC, null until first modification |
| `Settings_LayerHeight` | `numeric` | yes | owned type `PrintSettings`, same table |
| `Settings_InfillPercentage` | `integer` | yes | |
| `Settings_NozzleSize` | `integer` | yes | |
| `Settings_PrintTimeMinutes` | `integer` | yes | |
| `Settings_FilamentUsedGrams` | `numeric` | yes | |
| `Settings_SupportsRequired` | `boolean` | no | |
| `Size_WidthMm` | `numeric` | no | owned type `Dimensions`, same table |
| `Size_HeightMm` | `numeric` | no | |
| `Size_DepthMm` | `numeric` | no | |

#### Table `ProductImages`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK |
| `ProductId` | `uuid` | no | FK -> `Products.Id`, `ON DELETE CASCADE`; index |
| `ObjectKey` | `text` | no | MinIO object key |
| `Url` | `text` | no | public URL computed at upload time and stored |
| `IsPrimary` | `boolean` | no | |

#### Table `ProductAttributes`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK |
| `ProductId` | `uuid` | no | FK -> `Products.Id`, `ON DELETE CASCADE`; index |
| `Key` | `varchar(100)` | no | unique together with `ProductId` (`IX_ProductAttributes_ProductId_Key`) |
| `Value` | `varchar(500)` | no | |

#### Table `ProductRelations`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK |
| `ProductId` | `uuid` | no | FK -> `Products.Id`, `ON DELETE CASCADE`; index |
| `RelatedProductId` | `uuid` | no | **no FK** (indexed); incoming relations are removed explicitly when the target product is deleted |
| `Type` | `varchar(50)` | no | unique together with `ProductId` and `RelatedProductId` |

There are no JSON/JSONB columns, no database enums and no stored procedures. Reads of a single product use
`Include(Images, Attributes, RelatedProducts)` with split queries.

### Redis (cache)

Redis is used only as a read-through cache (JSON strings; `StringSet` with TTL). The service registers the cache only
if `Redis:ConnectionString` is non-empty; all handlers depend on `ICacheService`, so in practice Redis is required.
The multiplexer uses `AbortOnConnectFail=false` and `ConnectRetry=10`.

| Key | Value | TTL | Written by | Removed by |
|---|---|---|---|---|
| `product:{productId}` | `System.Text.Json` (default options, so PascalCase property names) serialization of `ProductDto` | 10 minutes | `GET /products/{id}` | update, delete, add/delete attribute, add/delete relation |
| `products:{SHA-256 hex}` | JSON serialization of `PaginatedListDto<ProductDto>`; the hash is of the JSON-serialized `ProductFilter` (category, price range, tags, material, printer type, search, status, page, page size) | 5 minutes | `GET /products` | all of the above plus create (by prefix scan `products:*`) |

`RemoveByPrefixAsync` enumerates keys of the first endpoint with `IServer.Keys(pattern)` and deletes them one by one.
`RedisOptions.DefaultTtlSeconds` (default 300) is used only when a caller passes no TTL; the current handlers always pass one.
Image upload/delete do not invalidate any key.

### MinIO (object storage)

| Aspect | Details |
|---|---|
| Bucket | `Minio:Bucket`, default `products` |
| Bucket creation | at startup in `Development` and lazily before each upload if missing; no bucket policy is applied by the code (objects are not made public by the service) |
| Object key | `products/{productId}/{random-guid}-{originalFileName}` (file name is used as sent) |
| Object content type | taken from the uploaded file's content type |
| Public URL (stored in `ProductImages.Url`) | `{PublicEndpoint or Endpoint}/{bucket}/{objectKey}`; `http://` is prepended when no scheme is present. Example: `http://localhost:9000/products/products/<productId>/<guid>-vase.jpg` |
| Client | `Endpoint` with `http(s)://` stripped, SSL always disabled, credentials from `AccessKey`/`SecretKey` |
| Deletion | object is removed on image deletion and for every image on product deletion |
| Tracing | `Catalog.Storage` activities `minio.upload` / `minio.delete` (see [Observability](#observability)) |

---

## Domain model and business rules

### Aggregate

`Product` (aggregate root) owns `ProductImage`, `ProductAttribute` and `ProductRelation` children and two value
objects (`PrintSettings`, `Dimensions`).

| Entity / value object | Members |
|---|---|
| `Product` | `Id`, `Name`, `Slug`, `ShortDescription`, `LongDescription`, `Price`, `Currency` (entity default `"EUR"`), `Status`, `CategoryId`, `Settings`, `Size`, `Tags`, `SupportedMaterials`, `CompatiblePrinters`, `Images`, `Attributes`, `RelatedProducts`, `DownloadCount`, `AverageRating`, `CreatedAt`, `UpdatedAt` |
| `ProductImage` | `Id`, `ProductId`, `ObjectKey`, `Url`, `IsPrimary` |
| `ProductAttribute` | `Id`, `ProductId`, `Key`, `Value` |
| `ProductRelation` | `Id`, `ProductId`, `RelatedProductId`, `Type` |
| `PrintSettings` | `LayerHeight`, `InfillPercentage`, `NozzleSize`, `PrintTimeMinutes`, `FilamentUsedGrams`, `SupportsRequired` |
| `Dimensions` | `WidthMm`, `HeightMm`, `DepthMm` |

Enums: `MaterialType` = `PLA`, `ABS`, `PETG`, `Resin`, `Nylon`, `TPU`; `PrinterType` = `FDM`, `SLA`, `SLS`;
`ProductStatus` = `Draft`(0), `Active`(1), `Inactive`(2), `OutOfStock`(3), `Discontinued`(4).

### Rules implemented

* New products start as `Draft`, `CreatedAt = UtcNow`, `DownloadCount = 0`, `AverageRating = 0`.
* Slug = lower-cased name with spaces replaced by `-` and `.` / `,` removed. It is not made unique in code; the DB
  unique index rejects a second product with the same slug (surfaces as `500`).
* Tags are trimmed, blanks removed and de-duplicated (case-sensitive); materials and printers are de-duplicated.
* Attribute keys are unique per product (case-sensitive); relations are unique per (product, related product).
* Setting a primary image clears the primary flag on all other images of that product.
* `UpdatedAt` is set by `UpdateDetails` and the status transition methods.

### Status state machine

The `Product` entity defines transition methods, but **no endpoint or handler calls them**:

```
Draft ──Publish()──> Active ──Deactivate()──> Inactive ──Publish()──> Active
Any status ──MarkOutOfStock()──> OutOfStock          Any status ──Discontinue()──> Discontinued
```

`Publish()` is allowed only from `Draft` or `Inactive`; `Deactivate()` only from `Active`; `MarkOutOfStock()` and
`Discontinue()` have no guards. Through the HTTP API every product therefore stays `Draft`. The `OutOfStock` comment in
code refers to a future inventory integration; Catalog does not consume any inventory events.

### Background jobs

None. The service has no hosted services, schedulers or consumers.

---

## Configuration

Settings are read from `appsettings.json`, `appsettings.{Environment}.json` and environment variables
(use `__` as the hierarchy separator, for example `Database__ConnectionString`). Option classes are in
`Infrastructure/Configuration`. Defaults below are the values committed in `appsettings.json` (docker-network host
names) with the `appsettings.Development.json` override in the last column.

| Key / environment variable | Meaning | `appsettings.json` | `appsettings.Development.json` |
|---|---|---|---|
| `ServiceName` | OpenTelemetry resource service name (code fallback `catalog-service`) | `catalog-service` | `catalog-service` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP gRPC endpoint for traces and logs (code fallback `http://jaeger:4317`) | `http://jaeger:4317` | `http://localhost:4317` |
| `Database__ConnectionString` | Npgsql connection string | `Host=postgres;Port=5432;Database=catalog;Username=postgres;Password=postgres` | `Host=localhost;Port=5433;Database=catalog;Username=postgres;Password=postgres` |
| `RabbitMQ__Host` | broker host | `rabbitmq` | `localhost` |
| `RabbitMQ__Port` | broker port (class default `5672`) | `5672` | `5672` |
| `RabbitMQ__Username` / `RabbitMQ__Password` | broker credentials | `guest` / `guest` | `guest` / `guest` |
| `RabbitMQ__VirtualHost` | vhost (class default `/`) | `/` | `/` |
| `Minio__Endpoint` | `host:port` used by the SDK (scheme is stripped, SSL off) | `minio:9000` | `localhost:9000` |
| `Minio__PublicEndpoint` | base used to build public image URLs; falls back to `Minio__Endpoint` only when null | `http://localhost:9000` | `http://localhost:9000` |
| `Minio__AccessKey` / `Minio__SecretKey` | MinIO credentials | `minioadmin` / `minioadmin` | `minioadmin` / `minioadmin` |
| `Minio__Bucket` | bucket for product images | `products` | `products` |
| `Redis__ConnectionString` | StackExchange.Redis configuration string; empty disables the cache registration | `redis:6379` | `localhost:6379` |
| `Redis__DefaultTtlSeconds` | fallback TTL when no TTL is passed (class default) | `300` (not in file) | `300` (not in file) |
| `ASPNETCORE_ENVIRONMENT` | `Development` runs migrations and creates the bucket at startup; `Testing` (used by integration tests) disables health checks and the startup block | not set | `Development` (launch profile) |
| `ASPNETCORE_URLS` | listen URLs | not set (Docker image: set to `http://+:8080` by compose) | `http://localhost:5129` (launch profile) |

Values in `appsettings*.json` are development defaults only; do not reuse them outside local environments.

### docker-compose entry (`docker/docker-compose.yml`)

```yaml
catalog:
  build: { context: ../Catalog, dockerfile: Catalog/Dockerfile }
  container_name: catalog
  depends_on: [postgres, rabbitmq, minio, redis, jaeger]
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: http://+:8080
    Minio__PublicEndpoint: http://minio:9000
    Minio__AccessKey: ${MINIO_ROOT_USER}
    Minio__SecretKey: ${MINIO_ROOT_PASSWORD}
  ports: ["${CATALOG_PORT}:8080"]
```

Relevant `docker/template.env` values: `CATALOG_PORT=5134`, `POSTGRES_PORT=5433`, `REDIS_PORT=6379`,
`RABBITMQ_PORT=5672`, `RABBITMQ_UI_PORT=15672`, `MINIO_PORT=9000`, `MINIO_CONSOLE_PORT=9001`, `OTEL_UI_PORT=16686`,
`OTEL_GRPC_PORT=4317`. `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `MINIO_ROOT_USER` and `MINIO_ROOT_PASSWORD` are empty in
the template and must be filled in your local `docker/.env`.

---

## Observability

| Concern | Implementation |
|---|---|
| Tracing | OpenTelemetry tracing with ASP.NET Core (records exceptions), HttpClient (records exceptions), EF Core and StackExchange.Redis instrumentation, exported over OTLP (gRPC) to `OTEL_EXPORTER_OTLP_ENDPOINT` (Jaeger all-in-one in compose; UI at `http://localhost:16686`) |
| Custom activity sources | `Catalog.Messaging` (`rabbitmq.publish`, tags `messaging.system`, `messaging.destination`) and `Catalog.Storage` (`minio.upload`, `minio.delete`, tags `object.key`, `bucket`, `object.url`). These sources are **not registered** with `AddSource(...)` in `OpenTelemetrySetup`, so their spans are currently not exported |
| Metrics | none |
| Logging | console provider plus the OpenTelemetry logging provider (scopes, formatted message, parsed state) exporting to the same OTLP endpoint; default providers are cleared |
| Request logging | `RequestLoggingMiddleware` logs `HTTP {Method} {Path} \| CorrelationId: {CorrelationId}` at information level |
| MediatR logging | `LoggingBehavior` logs `Handling {Request} \| Payload: {json}`, the elapsed time, and errors. The full request payload is logged (including descriptions and local temp file paths) |
| Correlation id | `CorrelationIdMiddleware` reads or generates `X-Correlation-Id`, stores it in `HttpContext.Items`, and echoes it in the response header. It is only used in the request log line: it is not put into a logging scope, traces, or published events |
| Health | `GET /health` with checks `postgres` (Npgsql), `redis` (only if configured), `rabbitmq` (opens a new connection per check) and `minio` (bucket exists). Disabled when environment is `Testing`. `HealthCheckEndpoint.MapHealth` exists but is not used (the same mapping is inline in `Program.cs`). The Dockerfile defines no `HEALTHCHECK` |

---

## Running locally

### Prerequisites

* .NET SDK 10.0
* Docker with Docker Compose (for infrastructure and for integration tests)

### Option A: infrastructure in Docker, service native (recommended for development)

```powershell
# 1. Create docker/.env from the template and fill in RABBITMQ_*, MINIO_ROOT_*, NEO4J_* (if you start those services)
Copy-Item docker/template.env docker/.env

# 2. Start only what Catalog needs
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d postgres rabbitmq minio redis jaeger

# 3. Run the service (profile "http" -> http://localhost:5129, environment Development)
dotnet run --project Catalog/Catalog/Catalog.csproj --launch-profile http
```

`appsettings.Development.json` points at `localhost` with these host ports: PostgreSQL `5433`, RabbitMQ `5672`,
MinIO `9000`, Redis `6379`, OTLP `4317`. Because the committed defaults use `guest/guest` for RabbitMQ and
`minioadmin/minioadmin` for MinIO, either set those same values in `docker/.env` or override
`RabbitMQ__Username`, `RabbitMQ__Password`, `Minio__AccessKey`, `Minio__SecretKey` for the native process. On start in
`Development` the service applies EF migrations (creating the `catalog` database if needed) and creates the MinIO bucket.

The `https` launch profile additionally listens on `https://localhost:7191`.

Try it with `Catalog/Catalog/Catalog.http` (variable `@host = http://localhost:5129`) or:

```powershell
curl http://localhost:5129/health
curl "http://localhost:5129/products?page=1&pageSize=10"
```

### Option B: whole service in Docker Compose

```powershell
docker compose -f docker/docker-compose.yml --env-file docker/.env up -d --build catalog
```

Container port `8080` is published on host port `${CATALOG_PORT}` (`5134` in `template.env`), for example
`http://localhost:5134/products?page=1&pageSize=10`. See the note on environment layering in
[Known gaps](#known-gaps-and-inconsistencies) before relying on this path.

Docker image: build context is the `Catalog/` folder; the `Dockerfile` restores using `Catalog.sln` and the project
files, publishes `Catalog.csproj` in Release, and runs `dotnet Catalog.dll` from `aspnet:10.0` (exposes `8080`).

### Database migrations

Migrations are in `Infrastructure/Persistence/Migrations`. To add one (from the `Catalog/` solution folder):

```powershell
dotnet ef migrations add <Name> --project Infrastructure --startup-project Catalog
```

(`Microsoft.EntityFrameworkCore.Design` is referenced by both projects; the `dotnet-ef` tool must be installed.)

---

## Tests

```powershell
dotnet test Catalog/Catalog.sln          # everything
dotnet test Catalog/UnitTests            # unit tests only (no Docker needed)
dotnet test Catalog/IntegrationTests     # needs a running Docker daemon
```

| Project | Scope | Needs |
|---|---|---|
| `UnitTests` | MediatR handlers with Moq (create, delete product incl. event publishing and cache invalidation, delete image/attribute/relation, add attribute/relation, upload image, get product by id / products / attributes / relations incl. cache hit and miss), `ProductRepository` (SQLite in-memory), `RedisCacheService`, `MinioFileStorageService` | nothing external. `UnitTest1.cs` is an empty placeholder; `Helpers/DbContextFactory.CreateDbContext` is private and unused |
| `IntegrationTests` | HTTP endpoint tests through `WebApplicationFactory<Program>`: create product, get by id, delete product (incl. removal of incoming relations and attributes), upload image, add attribute | Docker: `CatalogApiFactory` starts Testcontainers for `postgres:17`, `redis:7`, `rabbitmq:3-management` and `minio/minio:latest`, sets environment `Testing` (no health checks, no startup migration), runs `Database.Migrate()` itself and injects connection settings via in-memory configuration |

Test coverage is partial: there are no tests for `PUT /products/{id}`, the list endpoint with filters, relation
endpoints at HTTP level, or event payload contents.

---

## Security notes

* The service has **no authentication or authorization**. Do not expose it directly; route traffic through the gateway
  or another component that authenticates callers and restricts write operations (`POST`/`PUT`/`DELETE`).
* CORS is not configured in this service.
* Uploads: 5 MiB limit, no content-type or magic-byte validation, original file name is embedded in the object key
  without sanitization; antiforgery is disabled on the upload endpoint.
* No bucket policy is set by the code. Stored image URLs are plain `http://` URLs of the MinIO endpoint, so for
  browsers to load them the bucket (or the objects) must be readable anonymously by configuration done outside this service.
* Credentials in `appsettings*.json` are local-development defaults; supply real values via environment variables or a
  secret store. No secrets are required by the code beyond the database, RabbitMQ and MinIO credentials.
* `LoggingBehavior` logs complete request payloads.

---

## Known gaps and inconsistencies

Observed directly in the code:

1. **Print settings and dimensions are not bound from requests.** `PrintSettings`/`Dimensions` use `private set` and
   are deserialized by `System.Text.Json`, which skips non-public setters. Values in `settings`/`size` are ignored, so
   stored values are always defaults.
2. **Product status can never leave `Draft` via the API.** `Publish`, `Deactivate`, `MarkOutOfStock` and `Discontinue`
   exist on the entity but nothing calls them, and there is no endpoint to change status. `GET /products` has no default
   status filter, so drafts are returned to every caller.
3. **No authentication/authorization** at the service level.
4. **`product.image.uploaded` is incomplete**: `ImageId` is always an empty GUID and `IsPrimary` is always `false`.
5. **`product.updated` truncates `AverageRating`** to an integer, and omits `CategoryId`.
6. **`PUT` resets `downloadCount` and `averageRating` to `0`** when they are omitted (the request record defaults them to
   `0`, which the handler treats as a provided value).
7. **Cache is not invalidated by image upload/delete** (stale product/list for up to 10 / 5 minutes), and adding or
   removing a relation invalidates only the source product's `product:{id}` key (plus `products:*`).
8. **List endpoint performance:** `GET /products` loads every product matching the SQL-level filters with all children,
   filters tags/material/printer type in memory, paginates in memory, and executes the whole query twice (items and
   count). `page` and `pageSize` are effectively required query parameters and there is no maximum page size.
9. **Error mapping:** duplicate attribute/relation (`InvalidOperationException`), slug collisions and other
   `DbUpdateException`s are returned as `500` instead of `409`. Missing `currency`, `settings` or `size` in the body
   is not validated and ends in an unhandled error. There is no input validation for name, price or lengths.
10. **No transactional outbox:** events are published after the commit; a broker failure yields `500` after the data
    changed and the event is lost. A new channel is created and the exchange is re-declared for every message; messages
    are not explicitly persistent and there are no publisher confirms. The RabbitMQ connection is created synchronously
    on first use without retry.
11. **Image/product deletion order:** MinIO objects are deleted before the database commit, so a failed commit leaves
    rows pointing to deleted objects. Deleting a non-primary-aware image does not promote another primary image.
12. **Docker Compose environment layering (likely broken as committed):** compose sets `ASPNETCORE_ENVIRONMENT=Development`,
    so `appsettings.Development.json` (with `localhost` hosts and DB port `5433`) overrides the docker-network defaults in
    `appsettings.json`, and compose only overrides the three `Minio__*` values. Inside the container the database,
    RabbitMQ, Redis, MinIO endpoint and OTLP endpoint would therefore point to `localhost`. Other services in compose set
    `Database__*`, `RabbitMQ__*` and `Redis__*` explicitly; Catalog does not. Additionally `Minio__PublicEndpoint` is
    `http://minio:9000` in compose, so stored image URLs use a Docker-internal host name that browsers on the host cannot
    resolve (the committed default is `http://localhost:9000`). Also, `docker/template.env` leaves `RABBITMQ_USER/PASSWORD`
    and `MINIO_ROOT_USER/PASSWORD` empty while `appsettings.json` hard-codes `guest/guest` and `minioadmin/minioadmin`.
13. **Unused / placeholder code:** `Application/Common/Result.cs`, `Errors.cs`, `Domain/Exceptions/DomainException.cs` (empty
    classes), `Domain/Commons/ProductFile.cs` (unmapped), `HealthCheckEndpoint.MapHealth` (unused),
    `ProductRepository.DeleteImageAsync` (not on the interface), `Microsoft.AspNetCore.OpenApi` (no OpenAPI endpoint),
    and the `Catalog.Messaging`/`Catalog.Storage` activity sources (not registered with OpenTelemetry).
14. **Catalog.http** contains a create example without `currency`, `settings` and `size`, and the "delete image" example
    reuses the `attributeId` variable as the image id.
15. **Naming/layout oddities:** `ProductAttribute` and `ProductRelation` live in the `Domain.Commons` namespace but in
    the `Entities` folder; enums `MaterialType`/`PrinterType` are in `Domain.Commons`, `ProductStatus` in `Domain.Enums`;
    migrations `FixProduct3DStructure2/3` are empty.
