# PdfWorker

Event-driven document generation service for the 3D-printing e-commerce platform. It listens to payment events on RabbitMQ, builds an **invoice**, **receipt** or **credit note** PDF with QuestPDF, stores the PDF in MinIO, records metadata in PostgreSQL and announces the result on RabbitMQ. It also exposes a small read-only HTTP API for looking up document metadata.

> This README documents what the code in `PdfWorker/` actually does. Items that look inconsistent or unfinished are collected in [Known gaps and inconsistencies](#13-known-gaps-and-inconsistencies).

## Table of contents

1. [Purpose and role in the system](#1-purpose-and-role-in-the-system)
2. [Tech stack](#2-tech-stack)
3. [Architecture and project layout](#3-architecture-and-project-layout)
4. [HTTP API](#4-http-api)
5. [Messaging (RabbitMQ)](#5-messaging-rabbitmq)
6. [Data storage](#6-data-storage)
7. [Document generation flow](#7-document-generation-flow)
8. [Configuration](#8-configuration)
9. [Observability](#9-observability)
10. [Running locally](#10-running-locally)
11. [Tests](#11-tests)
12. [Docker image](#12-docker-image)
13. [Known gaps and inconsistencies](#13-known-gaps-and-inconsistencies)

---

## 1. Purpose and role in the system

| Item | Value |
|---|---|
| Service name (`ServiceName`, OTel resource) | `pdf-worker` |
| Compose service / container | `pdf-worker` |
| Container port | `8080` |
| Host port | `${PDF_WORKER_PORT}` (`5140` in `docker/template.env`); `dotnet run` uses `http://localhost:5140` (`launchSettings.json`) |
| Datastores | PostgreSQL database `pdfworker` (table `documents`), MinIO bucket `documents` |
| Broker | RabbitMQ (consumes `payments-exchange`, publishes `documents-exchange`) |

What it does:

- Consumes `payment.completed` and generates **two** documents per payment: an invoice and a receipt.
- Consumes `payment.refunded` and generates **one** credit note per refund.
- Uploads each PDF to MinIO and persists a `documents` row (status, storage location, request snapshot).
- Publishes `document.generated` or `document.generation.failed` to `documents-exchange`.
- Serves metadata via `GET /documents` and `GET /documents/{documentId}`. The service has **no endpoint that returns the PDF bytes**.

Relationships with other services:

| Service | Direction | How |
|---|---|---|
| Payments | inbound | Publishes `payment.completed` / `payment.refunded` on `payments-exchange`. PdfWorker references the shared project `Payments/Contracts` (namespace `Contracts.Events`) for these event types. |
| Orders | outbound (HTTP) | `GET {Orders:BaseUrl}/orders/{orderId}` to enrich documents with line items and shipping address. |
| API Gateway | inbound (HTTP) | YARP route `pdf-worker-documents` forwards `/documents/{**catch-all}` to `http://pdf-worker:8080`. |
| MinIO / PostgreSQL / RabbitMQ / Jaeger | infrastructure | See sections 5, 6, 9. |
| Notifications / others | outbound (events) | `documents-exchange` is declared and published to, but no queue is bound by this service, and no other service in this repository references `documents-exchange`, `document.generated` or `document.generation.failed`. |

---

## 2. Tech stack

| Area | Technology |
|---|---|
| Runtime / language | .NET 10 (`net10.0`), C# with nullable reference types and implicit usings |
| Web | ASP.NET Core Minimal APIs (`Microsoft.NET.Sdk.Web`) plus `BackgroundService` consumers |
| CQRS / mediator | MediatR 13.0.0 (with a logging pipeline behavior) |
| Validation | FluentValidation 11.11.0 |
| Resilience | Polly 8.5.2 (pipeline retries); `Microsoft.Extensions.Http.Resilience` 10.1.0 (Orders HTTP client) |
| PDF | QuestPDF 2025.7.0 (`LicenseType.Community`) |
| Persistence | EF Core 10.0.6, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1 |
| Object storage | `Minio` 7.0.0 |
| Messaging | `RabbitMQ.Client` 7.2.1 |
| Health checks | `AspNetCore.HealthChecks.NpgSql` 9.0.0, `AspNetCore.HealthChecks.Rabbitmq` 9.0.0, custom `MinioHealthCheck` |
| Observability | OpenTelemetry 1.15.x (ASP.NET Core, HttpClient, EF Core instrumentation, OTLP exporter) |
| Tests | xUnit 2.9.3, FluentAssertions 8.9.0, Moq 4.20.72, Testcontainers 4.11.0 (PostgreSQL, RabbitMQ, MinIO), `Xunit.SkippableFact`, `Microsoft.AspNetCore.Mvc.Testing` |

---

## 3. Architecture and project layout

Clean-architecture style, split into five projects plus two test projects (`PdfWorker.sln` / `PdfWorker.slnx`).

```
PdfWorker/
├── PdfWorker/                    # Host (Web SDK): composition root, HTTP endpoints, middleware
│   ├── Program.cs
│   ├── Endpoints/                # DocumentEndpoints.cs (list/get), MinioHealthCheck.cs
│   ├── Middleware/               # CorrelationId, RequestLogging, Exception middleware
│   ├── Properties/launchSettings.json
│   ├── appsettings.json / appsettings.Development.json
│   └── Dockerfile
├── Application/                  # Use cases (no infrastructure dependencies)
│   ├── Abstractions/             # IDocumentRepository, IPdfRenderer, IObjectStorageService,
│   │                             #   IEventPublisher, IOrderEnrichmentClient (+ DTOs)
│   ├── Common/                   # IdempotencyKeyBuilder, LoggingBehavior (MediatR)
│   ├── Contracts/                # Versioned document-request contracts (…V1 classes)
│   ├── DependencyInjection/      # AddApplication()
│   ├── Features/
│   │   ├── Documents/            # GenerateDocumentPipeline + GenerateInvoice / GenerateReceipt /
│   │   │                         #   GenerateCreditNote (command, handler, validator),
│   │   │                         #   GetDocumentById, ListDocuments
│   │   └── Events/               # HandlePaymentCompleted, HandlePaymentRefunded
│   └── Mapping/                  # PaymentEventToDocumentRequestMapper (+ SellerOptions)
├── Domain/                       # Document entity, DocumentType / GenerationStatus enums,
│                                 #   DocumentException hierarchy
├── Contracts/                    # Published event contracts + ActivitySources (PdfWorker.Contracts)
│   └── Events/                   # DocumentGenerated, DocumentGenerationFailed
├── Infrastructure/
│   ├── Configuration/            # Options classes (Database, RabbitMq, Minio, Orders, Enrichment)
│   ├── DependencyInjection/      # AddInfrastructure(), RabbitMqTopologyInitializer
│   ├── Enrichment/               # OrdersHttpClient
│   ├── Messaging/                # ConsumerBase, RabbitMqPublisher, Consumers/*
│   ├── Observability/            # OpenTelemetrySetup
│   ├── Persistence/              # PdfWorkerDbContext, DocumentConfiguration, DocumentRepository,
│   │                             #   Migrations/, PdfWorkerDbContextFactory (design-time)
│   ├── Rendering/                # QuestPdfRenderer, Templates/{Invoice,Receipt,CreditNote}DocumentV1
│   └── Storage/                  # MinioObjectStorageService
├── UnitTests/                    # xUnit + Moq
└── IntegrationTests/             # xUnit + Testcontainers + WebApplicationFactory
```

Project references: `PdfWorker` -> `Application`, `Infrastructure`; `Infrastructure` -> `Application`, `Domain`, `Contracts`; `Application` -> `Domain`, `Contracts`, `Payments/Contracts`.

Key design points:

- Each consumed event is turned into a MediatR command (`HandlePaymentCompletedCommand`, `HandlePaymentRefundedCommand`), which maps the event (plus Orders data) to a versioned document request and dispatches `GenerateInvoiceCommand` / `GenerateReceiptCommand` / `GenerateCreditNoteCommand`.
- All three generation handlers delegate to the shared `GenerateDocumentPipeline` (idempotency, render, upload, persist, publish).
- Template selection is driven by the request's `TemplateVersion` string (`invoice-v1`, `receipt-v1`, `credit-note-v1`).
- Domain errors derive from `DocumentException` (`Code`, `IsTransient`): `DocumentValidationException`, `DocumentNotFoundException`, `DocumentRenderException`, `DocumentStorageException` (transient by default), `OrderEnrichmentException` (transient by default).

---

## 4. HTTP API

Base URL: `http://localhost:5140` (direct) or `http://localhost:${GATEWAY_PORT}` (gateway, `5133` in `template.env`).

The service itself performs **no authentication or authorization** (no JWT middleware, no `[Authorize]`, no per-user filtering). Anyone who can reach it can read any document's metadata.

### Gateway exposure

`ApiGateway/ApiGateway/appsettings*.json` defines:

| Route | Match | Cluster | Destination |
|---|---|---|---|
| `pdf-worker-documents` | `/documents/{**catch-all}` | `pdf-worker-cluster` | `http://pdf-worker:8080` |

There is no path transform, so the same paths work through the gateway (`/documents`, `/documents/{id}`). The route entry has no authorization policy configured in `appsettings`. Whether the gateway applies global authentication was not verified from this folder. `/health` is not routed by the gateway.

### Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/documents` | none | List document metadata (paged, newest first) |
| `GET` | `/documents/{documentId}` | none | Get one document's metadata |
| `GET` | `/health` | none | Health of PostgreSQL, RabbitMQ, MinIO |

#### `GET /documents`

Query parameters:

| Name | Type | Required | Notes |
|---|---|---|---|
| `orderId` | GUID | one of `orderId` / `paymentId` | Filter by order |
| `paymentId` | GUID | one of `orderId` / `paymentId` | Filter by payment |
| `documentType` | enum | no | `Invoice`, `Receipt`, `CreditNote` |
| `status` | enum | no | `Pending`, `Completed`, `Failed` |
| `page` | int | see note | `<= 0` becomes `1` |
| `pageSize` | int | see note | `<= 0` becomes `20`; values above `100` are capped to `100` |

Note: `page` and `pageSize` are declared as non-nullable `int` parameters without default values in `DocumentEndpoints.cs`. Under ASP.NET Core minimal API binding rules that makes them required query parameters, so clients should always send them (for example `page=1&pageSize=20`). The `<= 0` fallbacks only apply to supplied values. This was not verified at runtime. See [Known gaps](#13-known-gaps-and-inconsistencies).

Results are ordered by `CreatedAt` descending.

Example request:

```http
GET /documents?orderId=3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11&page=1&pageSize=20
```

Response `200 OK` (camelCase, enums serialized as strings):

```json
{
  "items": [
    {
      "id": "0b2f6a4e-5c1d-4e7a-9b1f-2d3c4e5f6a7b",
      "documentType": "Invoice",
      "idempotencyKey": "invoice:3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11:8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
      "templateVersion": "invoice-v1",
      "status": "Completed",
      "storageObjectKey": "documents/invoice/2026/06/9f86d081884c7d65.pdf",
      "storageBucket": "documents",
      "orderId": "3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11",
      "paymentId": "8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
      "userId": "a1b2c3d4-e5f6-4789-a0b1-c2d3e4f5a6b7",
      "correlationId": "5d0e3c52-6b1f-4c0a-8f3e-0a9b8c7d6e5f",
      "failureReason": null,
      "fileSizeBytes": 18342,
      "createdAt": "2026-06-18T10:52:43.123Z",
      "completedAt": "2026-06-18T10:52:44.001Z",
      "failedAt": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

Status codes:

| Code | When | Body |
|---|---|---|
| `200` | Success (an empty `items` array if nothing matches) | `DocumentListResponse` |
| `400` | Neither `orderId` nor `paymentId` supplied | `{ "title": "At least one of orderId or paymentId is required", "status": 400, "detail": "Provide orderId and/or paymentId query parameters" }` |
| `400` / `500` | Query binding errors (for example malformed GUID or enum, or missing `page` / `pageSize`) are produced by ASP.NET Core binding; the exact status depends on environment (see Known gaps) | framework / middleware dependent |
| `500` | Unhandled exception | error body below |

#### `GET /documents/{documentId}`

`documentId` must be a GUID (route constraint `:guid`; a non-GUID path matches no route and returns an empty `404`).

Response `200 OK`: a single `DocumentDto`, same shape as one element of `items` above.

| Code | When | Body |
|---|---|---|
| `200` | Found | `DocumentDto` |
| `404` | Unknown id | `DocumentNotFoundException` mapped by `ExceptionMiddleware` |

```json
{
  "title": "Document 0b2f6a4e-5c1d-4e7a-9b1f-2d3c4e5f6a7b not found",
  "status": 404,
  "detail": "Document 0b2f6a4e-5c1d-4e7a-9b1f-2d3c4e5f6a7b not found",
  "code": "document.not_found",
  "traceId": "0HN7..."
}
```

#### Error format (`ExceptionMiddleware`)

| Exception | Status | `code` |
|---|---|---|
| `DocumentNotFoundException` | 404 | `document.not_found` |
| `DocumentValidationException` / other `DocumentException` | 400 | e.g. `document.validation_failed` |
| any other `Exception` | 500 | `internal.error` |

Body: `{ "title", "status", "detail", "code", "traceId" }` (`title` and `detail` both carry the exception message, including for 500).

#### `GET /health`

Registered only when the environment is not `Testing`. Always returns JSON (`status` is the aggregate; `checks` maps check name to status):

```json
{
  "status": "Healthy",
  "checks": {
    "postgres": "Healthy",
    "rabbitmq": "Healthy",
    "minio": "Healthy"
  }
}
```

| Check | Implementation | Result |
|---|---|---|
| `postgres` | `AddNpgSql(Database:ConnectionString)` | Healthy / Unhealthy |
| `rabbitmq` | `AddRabbitMQ` opens a new connection using `RabbitMQ:*` | Healthy / Unhealthy |
| `minio` | `MinioHealthCheck` calls `BucketExistsAsync(Minio:Bucket)` | `Healthy` ("MinIO bucket reachable"), `Degraded` ("Bucket does not exist yet"; the bucket is created lazily on first upload), `Unhealthy` ("MinIO unreachable") |

HTTP status uses ASP.NET Core defaults (`Healthy` and `Degraded` return `200`, `Unhealthy` returns `503`). There is a single endpoint; no separate liveness/readiness endpoints.

---

## 5. Messaging (RabbitMQ)

Connection: a single shared `IConnection` (singleton, created when first resolved, i.e. at host start by the hosted services; the host fails to start if RabbitMQ is unreachable) configured from `RabbitMQ:*`. Each consumer opens its own channel; each publish opens a short-lived channel.

### Topology summary

| Direction | Exchange | Type | Durable | Routing key | Queue | Queue props |
|---|---|---|---|---|---|---|
| Consume | `payments-exchange` | topic | yes | `payment.completed` | `pdf-worker.payment-completed` | durable, non-exclusive, not auto-delete, no arguments |
| Consume | `payments-exchange` | topic | yes | `payment.refunded` | `pdf-worker.payment-refunded` | durable, non-exclusive, not auto-delete, no arguments |
| Publish | `documents-exchange` | topic | yes | `document.generated` | none (this service binds no queue) | n/a |
| Publish | `documents-exchange` | topic | yes | `document.generation.failed` | none | n/a |

- Consumers declare `payments-exchange` (topic, durable), declare their queue and bind it, so start-up order relative to Payments does not matter.
- `RabbitMqTopologyInitializer` (hosted service) declares `documents-exchange` at start-up, and `RabbitMqPublisher` re-declares it before each publish.
- `ConsumerBase<TMessage, TCommand>` uses manual acks (`autoAck: false`), no `BasicQos` (no prefetch limit) and no dead-letter exchange or TTL.

### Consumer behavior (`ConsumerBase`)

| Situation | Action |
|---|---|
| Body deserializes to `null` | Warning logged, **ack** (message dropped) |
| Handler completes | **ack** |
| `DocumentException` with `IsTransient == false` escapes the handler | Warning logged, **ack** (message dropped) |
| Any other exception (including transient `DocumentException`, JSON parse errors, DB errors) | Error logged, **nack with `requeue: true`** (immediate redelivery, no delay, no retry cap, no DLQ) |

Other details:

- Payloads are deserialized with `System.Text.Json` and `PropertyNameCaseInsensitive = true` (any property-name casing is accepted).
- Correlation id is read from the AMQP `CorrelationId` property, falling back to the `X-Correlation-Id` header (UTF-8 bytes). It is passed to commands, stored on the document and propagated on published events.
- Each message is handled in a new DI scope.
- Idempotency is handled in the pipeline (see [section 7](#7-document-generation-flow)), so redeliveries are safe.

### Consumed events

Types come from `Payments/Contracts/Events` (namespace `Contracts.Events`).

#### `payment.completed` -> `PaymentCompleted`

| Field | Type | Notes |
|---|---|---|
| `PaymentId` | `Guid` | |
| `OrderId` | `Guid` | |
| `UserId` | `Guid` | |
| `Amount` | `decimal` | Total paid; becomes invoice total and receipt amount |
| `Currency` | `string` | |
| `ProviderPaymentId` | `string` | Payment provider reference, printed on documents |
| `CompletedAt` | `DateTime` | Used as invoice/receipt issue date |
| `RecipientEmail` | `string?` | Customer email |

```json
{
  "paymentId": "8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "orderId": "3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11",
  "userId": "a1b2c3d4-e5f6-4789-a0b1-c2d3e4f5a6b7",
  "amount": 119.00,
  "currency": "EUR",
  "providerPaymentId": "pi_3Pxxxxxxxxxxxxxx",
  "completedAt": "2026-06-18T10:52:40Z",
  "recipientEmail": "jane@example.com"
}
```

Result: one **Invoice** and one **Receipt**.

#### `payment.refunded` -> `PaymentRefunded`

| Field | Type | Notes |
|---|---|---|
| `PaymentId` | `Guid` | |
| `OrderId` | `Guid` | |
| `UserId` | `Guid` | |
| `RefundAmount` | `decimal` | This refund |
| `TotalRefunded` | `decimal` | Cumulative refunded so far |
| `OriginalAmount` | `decimal` | Original payment amount |
| `Currency` | `string` | |
| `IsFullRefund` | `bool` | Full refund skips the Orders call and has no line items |
| `RefundedAt` | `DateTime` | Credit note issue date |
| `RecipientEmail` | `string?` | |

```json
{
  "paymentId": "8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "orderId": "3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11",
  "userId": "a1b2c3d4-e5f6-4789-a0b1-c2d3e4f5a6b7",
  "refundAmount": 30.00,
  "totalRefunded": 30.00,
  "originalAmount": 119.00,
  "currency": "EUR",
  "isFullRefund": false,
  "refundedAt": "2026-06-19T08:15:00Z",
  "recipientEmail": "jane@example.com"
}
```

Result: one **Credit note**.

### Published events

Published by `RabbitMqPublisher` to `documents-exchange` (topic, durable). Message properties: `ContentType = application/json`, `CorrelationId` and header `X-Correlation-Id` (only when a correlation id is known). Messages are serialized with default `System.Text.Json` options, so **property names are PascalCase** on the wire. Publishing does not use publisher confirms, `mandatory`, or persistent delivery mode.

#### `document.generated` -> `PdfWorker.Contracts.Events.DocumentGenerated`

Published after a document reaches `Completed`, and **again** whenever a duplicate request for an already `Completed` document is processed (idempotent replay).

| Field | Type | Notes |
|---|---|---|
| `DocumentId` | `Guid` | |
| `DocumentType` | `string` | `Invoice` / `Receipt` / `CreditNote` |
| `IdempotencyKey` | `string` | |
| `TemplateVersion` | `string` | |
| `OrderId` | `Guid` | |
| `PaymentId` | `Guid` | |
| `UserId` | `Guid` | |
| `StorageBucket` | `string` | |
| `StorageObjectKey` | `string` | |
| `ContentType` | `string` | `application/pdf` |
| `FileSizeBytes` | `long` | |
| `CorrelationId` | `string?` | |
| `GeneratedAt` | `DateTime` | Document `CompletedAt` |

```json
{
  "DocumentId": "0b2f6a4e-5c1d-4e7a-9b1f-2d3c4e5f6a7b",
  "DocumentType": "Invoice",
  "IdempotencyKey": "invoice:3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11:8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "TemplateVersion": "invoice-v1",
  "OrderId": "3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11",
  "PaymentId": "8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "UserId": "a1b2c3d4-e5f6-4789-a0b1-c2d3e4f5a6b7",
  "StorageBucket": "documents",
  "StorageObjectKey": "documents/invoice/2026/06/9f86d081884c7d65.pdf",
  "ContentType": "application/pdf",
  "FileSizeBytes": 18342,
  "CorrelationId": "5d0e3c52-6b1f-4c0a-8f3e-0a9b8c7d6e5f",
  "GeneratedAt": "2026-06-18T10:52:44.001Z"
}
```

#### `document.generation.failed` -> `PdfWorker.Contracts.Events.DocumentGenerationFailed`

Published when request validation fails, or when generation fails with a non-transient error.

| Field | Type | Notes |
|---|---|---|
| `DocumentType` | `string` | `Invoice` / `Receipt` / `CreditNote` |
| `IdempotencyKey` | `string` | |
| `OrderId` | `Guid` | |
| `PaymentId` | `Guid` | |
| `UserId` | `Guid` | |
| `SourceRoutingKey` | `string` | `payment.completed` or `payment.refunded` |
| `FailureReason` | `string` | Validation messages joined with `"; "`, or exception message |
| `CorrelationId` | `string?` | |
| `FailedAt` | `DateTime` | `DateTime.UtcNow` at publish time |

```json
{
  "DocumentType": "Invoice",
  "IdempotencyKey": "invoice:3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11:8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "OrderId": "3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11",
  "PaymentId": "8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "UserId": "a1b2c3d4-e5f6-4789-a0b1-c2d3e4f5a6b7",
  "SourceRoutingKey": "payment.completed",
  "FailureReason": "'Customer Address Line1' must not be empty.",
  "CorrelationId": "5d0e3c52-6b1f-4c0a-8f3e-0a9b8c7d6e5f",
  "FailedAt": "2026-06-18T10:52:41.500Z"
}
```

### Document request contracts (versioned `V1`)

These are internal contracts (plain classes with `init` properties in `Application/Contracts`), built by `PaymentEventToDocumentRequestMapper` from events and serialized (camelCase) into `documents.RequestContractJson`. They are not accepted over HTTP or RabbitMQ directly. All carry `ContractVersion = "v1"` and a `TemplateVersion`.

Shared types:

| Type | Fields |
|---|---|
| `MoneyV1` | `Amount` (decimal), `Currency` (string) |
| `SellerV1` | `LegalName`, `VatId`, `Email`, `AddressLine1`, `City`, `PostalCode`, `Country` (all string, non-null) |
| `PartyV1` (customer) | `Name`, `Email` (string); `AddressLine1`, `AddressLine2`, `City`, `State`, `PostalCode`, `Country` (string?) |
| `LineItemV1` | `ProductId` (Guid), `Description` (string), `Quantity` (int), `UnitPrice` (`MoneyV1`), `LineTotal` (`MoneyV1`) |

| Contract | `TemplateVersion` | Fields |
|---|---|---|
| `InvoiceDocumentRequestV1` | `invoice-v1` | `DocumentNumber`, `IssueDate`, `OrderId`, `PaymentId`, `ProviderPaymentId`, `Seller`, `Customer`, `LineItems`, `Subtotal`, `Tax`, `TaxRatePercent` (decimal), `Total`, `PaymentMethodSummary` (default `"Card"`) |
| `ReceiptDocumentRequestV1` | `receipt-v1` | `ReceiptNumber`, `IssueDate`, `OrderId`, `PaymentId`, `ProviderPaymentId`, `Seller`, `Customer`, `AmountPaid`, `PaymentMethodSummary` (default `"Card"`), `PaidAt` |
| `CreditNoteDocumentRequestV1` | `credit-note-v1` | `CreditNoteNumber`, `IssueDate`, `OrderId`, `PaymentId`, `OriginalPaymentAmount`, `RefundAmount`, `CumulativeRefunded`, `RemainingBalance`, `IsFullRefund`, `Seller`, `Customer`, `LineItems`, `Reason` (string?, never populated by the mapper and not rendered) |

Example stored snapshot (invoice):

```json
{
  "contractVersion": "v1",
  "templateVersion": "invoice-v1",
  "documentNumber": "INV-2026-8C1E5D7A",
  "issueDate": "2026-06-18T10:52:40Z",
  "orderId": "3f6c1a52-0a58-4b0d-9d2e-7d5f6d1a9c11",
  "paymentId": "8c1e5d7a-2b4f-4a6c-9e8d-1f2a3b4c5d6e",
  "providerPaymentId": "pi_3Pxxxxxxxxxxxxxx",
  "seller": {
    "legalName": "3D Print Shop GmbH",
    "vatId": "DE123456789",
    "email": "billing@example.com",
    "addressLine1": "Industriestr. 1",
    "city": "Munich",
    "postalCode": "80331",
    "country": "DE"
  },
  "customer": {
    "name": "Jane Doe",
    "email": "jane@example.com",
    "addressLine1": "Main Street 1",
    "addressLine2": null,
    "city": "Berlin",
    "state": null,
    "postalCode": "10115",
    "country": "DE"
  },
  "lineItems": [
    {
      "productId": "7e6d5c4b-3a29-4180-9f7e-6d5c4b3a2910",
      "description": "PLA Filament 1kg",
      "quantity": 2,
      "unitPrice": { "amount": 50.00, "currency": "EUR" },
      "lineTotal": { "amount": 100.00, "currency": "EUR" }
    }
  ],
  "subtotal": { "amount": 100.00, "currency": "EUR" },
  "tax": { "amount": 19.00, "currency": "EUR" },
  "taxRatePercent": 19,
  "total": { "amount": 119.00, "currency": "EUR" },
  "paymentMethodSummary": "Card"
}
```

#### Validation rules (FluentValidation)

| Contract | Rules |
|---|---|
| Invoice (`InvoiceDocumentRequestValidator`) | `ContractVersion == "v1"`; `TemplateVersion == "invoice-v1"`; `OrderId`, `PaymentId` not empty; `Seller` not null and `Seller.LegalName` not empty; `Customer` not null; `Customer.Name` not empty; `Customer.Email` not empty and a valid email; **`Customer.AddressLine1` not empty**; `LineItems` not empty; `Total` not null and `Total.Currency` not empty; for each line item `LineTotal.Currency == UnitPrice.Currency` |
| Receipt (`ReceiptDocumentRequestValidator`) | `ContractVersion == "v1"`; `TemplateVersion == "receipt-v1"`; `OrderId`, `PaymentId` not empty; `Seller` and `Customer` not null; `Customer.Email` not empty and valid; `AmountPaid` not null and `AmountPaid.Currency` not empty |
| Credit note (`CreditNoteDocumentRequestValidator`) | `ContractVersion == "v1"`; `TemplateVersion == "credit-note-v1"`; `OrderId`, `PaymentId` not empty; `RefundAmount`, `CumulativeRefunded`, `RemainingBalance` not null; `Customer` not null; `Customer.Email` not empty and valid; `LineItems` not empty **unless** `IsFullRefund` |

There is no arithmetic validation (no check that line totals, subtotal, tax and total are consistent). `InvoiceDocumentRequestValidator.ValidateTotalAmount(request, expected)` (0.01 tolerance) exists but is only called from unit tests.

On a validation failure the handler does **not** throw: it persists a `Failed` document (if none exists for the idempotency key; `RequestContractJson` is `null`), publishes `document.generation.failed`, and the message is acked.

---

## 6. Data storage

### PostgreSQL (database `pdfworker`)

Single `DbContext` (`PdfWorkerDbContext`) with one aggregate, `Document`, mapped by `DocumentConfiguration`. Column names are the C# property names (PascalCase); there is no snake_case naming convention.

Model as configured in `DocumentConfiguration`:

Table `documents`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | Primary key (`Guid.NewGuid()`) |
| `DocumentType` | `varchar(32)` | no | Enum stored **as string**: `Invoice`, `Receipt`, `CreditNote` |
| `IdempotencyKey` | `varchar(256)` | no | Unique |
| `TemplateVersion` | `varchar(32)` | no | `invoice-v1`, `receipt-v1`, `credit-note-v1` |
| `Status` | `varchar(32)` | no | Enum stored **as string**: `Pending`, `Completed`, `Failed` |
| `StorageObjectKey` | `varchar(512)` | yes | MinIO object key (set on completion) |
| `StorageBucket` | `varchar(128)` | yes | MinIO bucket (set on completion) |
| `ContentType` | `varchar(64)` | yes | `application/pdf` |
| `FileSizeBytes` | `bigint` | yes | |
| `OrderId` | `uuid` | no | |
| `PaymentId` | `uuid` | no | |
| `UserId` | `uuid` | no | |
| `CorrelationId` | `varchar(128)` | yes | |
| `SourceRoutingKey` | `varchar(64)` | no | `payment.completed` or `payment.refunded` |
| `FailureReason` | `varchar(1024)` | yes | |
| `RequestContractJson` | `jsonb` | yes | camelCase JSON snapshot of the V1 request contract used to render (null for validation-failed documents) |
| `CreatedAt` | `timestamptz` | no | UTC |
| `CompletedAt` | `timestamptz` | yes | |
| `FailedAt` | `timestamptz` | yes | |

Indexes and constraints:

| Name | Columns | Kind |
|---|---|---|
| `PK_documents` (EF default) | `Id` | primary key |
| `UX_documents_idempotency_key` | `IdempotencyKey` | **unique** |
| `IX_documents_order_id` | `OrderId` | non-unique |
| `IX_documents_payment_id` | `PaymentId` | non-unique |
| `IX_documents_user_id` | `UserId` | non-unique |
| `IX_documents_status_created_at` | `Status`, `CreatedAt` | non-unique |

State transitions (`Document` entity):

- `CreatePending(...)` -> `Pending` (`CreatedAt = UtcNow`).
- `MarkCompleted(bucket, key, contentType, size)` -> `Completed`, sets storage fields and `CompletedAt`, clears `FailureReason` and `FailedAt`.
- `MarkFailed(reason)` -> `Failed`, sets `FailureReason` and `FailedAt`.

Important: the committed EF migration does not match this model. See [Known gaps](#13-known-gaps-and-inconsistencies). The only migration, `20260618105243_InitialCreate`, creates:

| Aspect | Migration (`InitialCreate` and model snapshot) | `DocumentConfiguration` |
|---|---|---|
| Table name | `"Documents"` | `documents` |
| `DocumentType`, `Status` | `integer` | `varchar(32)` strings |
| String columns | `text` | length-limited `varchar` |
| `RequestContractJson` | `text` | `jsonb` |
| Indexes | none (primary key only) | 1 unique + 4 non-unique |

Schema creation: in the `Development` environment (which is what `docker-compose.yml` sets), `Program.cs` runs `db.Database.Migrate()` at start-up. This creates the database if it does not exist. It is **not** run in other environments (and not in `Testing` by the host; the integration test factory calls it explicitly). `PdfWorkerDbContextFactory` provides design-time access for `dotnet ef` (reads `appsettings.json`, `appsettings.Development.json`, environment variables).

### MinIO (bucket `documents`)

| Aspect | Value |
|---|---|
| Bucket | `Minio:Bucket` (default `documents`); created automatically on first upload if missing |
| Object key | `documents/{documentType}/{yyyy}/{MM}/{hash}.pdf` |
| `{documentType}` | `DocumentType.ToString().ToLowerInvariant()`: `invoice`, `receipt`, `creditnote` |
| `{yyyy}` / `{MM}` | UTC year / month at upload time |
| `{hash}` | first 16 lowercase hex chars of the SHA-256 of the PDF bytes |
| Content type | `application/pdf` |
| Content | Raw PDF bytes generated by QuestPDF (uploaded from memory, no encryption or tags configured) |

Example: `documents/creditnote/2026/06/4a1f0c9b7d3e2a58.pdf` in bucket `documents`.

Because the key is derived from the content hash, byte-identical PDFs written in the same month map to the same object key. The key therefore starts with `documents/` inside the `documents` bucket. The full object location is stored in the `StorageBucket` and `StorageObjectKey` columns and echoed in `DocumentGenerated`. No pre-signed URLs or download endpoint exist in this service.

---

## 7. Document generation flow

```
payments-exchange
  payment.completed ──> PaymentCompletedConsumer ──> HandlePaymentCompletedHandler
                                                      ├─ Orders GET /orders/{orderId}  (enrichment)
                                                      ├─ GenerateInvoiceCommand  ──┐
                                                      └─ GenerateReceiptCommand  ──┤
  payment.refunded  ──> PaymentRefundedConsumer ──> HandlePaymentRefundedHandler   │
                                                      ├─ Orders GET /orders/{id} (only if NOT full refund)
                                                      └─ GenerateCreditNoteCommand ┤
                                                                                   v
                                                         GenerateDocumentPipeline
                                         idempotency check -> insert Pending -> render (QuestPDF)
                                         -> upload (MinIO) -> mark Completed -> publish event
documents-exchange: document.generated | document.generation.failed
```

### Event to document mapping

| Event | Documents | Idempotency key | `SourceRoutingKey` |
|---|---|---|---|
| `payment.completed` | Invoice (`invoice-v1`) | `invoice:{orderId}:{paymentId}` | `payment.completed` |
| `payment.completed` | Receipt (`receipt-v1`) | `receipt:{orderId}:{paymentId}` | `payment.completed` |
| `payment.refunded` | Credit note (`credit-note-v1`) | `refund:{paymentId}:{totalRefunded:F2}:{refundAmount:F2}` | `payment.refunded` |

Examples: `invoice:3f6c...:8c1e...`, `refund:8c1e5d7a-...:30.00:30.00`. A second partial refund with a different cumulative total or amount yields a new key and a new credit note. Amounts are formatted with `F2` using the current culture (`IdempotencyKeyBuilder`).

### Enrichment (Orders)

`OrdersHttpClient` calls `GET /orders/{orderId}` on `Orders:BaseUrl` and maps the JSON (case-insensitive) to `OrderEnrichmentDto`.

Expected response fields: `id`, `userId`, `totalAmount`, `currency`, `shippingAddress { street, city, state, country, zipCode, fullName }`, `items[] { productId, productName, unitPrice, quantity, currency, lineTotal }`. A `recipientEmail` property exists on the DTO but `OrdersHttpClient` never populates it from the response.

| Orders response | Result |
|---|---|
| `200` | DTO used for line items, customer name and address |
| `404` | `null`: documents are generated with fallback values (below) |
| other `4xx` | `OrderEnrichmentException` (non-transient) |
| `5xx`, timeout, network error, other exception | `OrderEnrichmentException` (transient) |

HTTP client settings: base address `Orders:BaseUrl`, `HttpClient.Timeout = Enrichment:OrdersTimeoutSeconds` (3 s, bounding the whole call including retries), standard resilience handler with `MaxRetryAttempts = 2` and total timeout 10 s. No `Authorization` header is sent. `Enrichment:RetryCount` is bound but not used.

Enrichment is called **outside** any try/catch in both event handlers, so an enrichment failure aborts the message: transient errors are nacked and requeued; non-transient ones are acked and dropped without creating a document or a failure event.

### Mapping rules (`PaymentEventToDocumentRequestMapper`)

| Field | Rule |
|---|---|
| Seller | From `Seller:*` configuration |
| Document number | `{prefix}-{current UTC year}-{first 8 chars of PaymentId, upper-case}` with prefix `INV` (invoice), `RCP` (receipt) or `CN` (credit note), for example `INV-2026-8C1E5D7A`. The year is the processing time, not the event time. The number is only stored inside `RequestContractJson` (no column). |
| Issue date | Event `CompletedAt` (invoice, receipt) or `RefundedAt` (credit note) |
| Customer name | `order.shippingAddress.fullName`, else `"Customer"` |
| Customer email | event `RecipientEmail`, else `order.RecipientEmail` (never set), else `"unknown@example.com"` |
| Customer address | From `order.shippingAddress` (`street` -> `AddressLine1`, `zipCode` -> `PostalCode`, plus `city`, `state`, `country`); all null without an order |
| Line items | From `order.items` when present; otherwise one fallback line `"Order items"`, quantity 1, `ProductId = Guid.Empty`, price = payment amount (invoice) or refund amount (credit note) |
| Invoice subtotal | Sum of line totals (rounded to 2 dp), or `Amount / 1.19` if the sum is `<= 0` |
| Invoice tax | `Amount - Subtotal` (a difference, not computed from the rate) |
| Invoice `TaxRatePercent` | Hard-coded `19` |
| Invoice total / receipt amount | Event `Amount` |
| `PaymentMethodSummary` | Default `"Card"` |
| Credit note | `OriginalPaymentAmount = OriginalAmount`, `RefundAmount`, `CumulativeRefunded = TotalRefunded`, `RemainingBalance = OriginalAmount - TotalRefunded`, `IsFullRefund`; line items empty for a full refund; for a partial refund line items are **all** order items at their original amounts (not prorated), or the single fallback line |

Consequence: if Orders returns `404` (or no shipping address), `Customer.AddressLine1` is null, which fails the invoice validator, so the invoice is recorded as `Failed` and `document.generation.failed` is published. The receipt validator does not require an address, so the receipt still succeeds.

### Pipeline behavior (`GenerateDocumentPipeline`)

1. Look up an existing row by `IdempotencyKey`:
   - `Completed`: no work; **re-publishes** `document.generated` and returns (replay).
   - `Failed`: returns without retrying; no event is re-published.
   - `Pending`: reuses the row and attempts generation again.
   - none: creates a `Pending` row (with `RequestContractJson`) and saves it.
2. Inside a Polly retry pipeline (`MaxRetryAttempts = 3`, exponential backoff starting at 1 s, no jitter; retries only `DocumentException` with `IsTransient == true`): render the PDF (`IPdfRenderer`), upload it (`IObjectStorageService`), `MarkCompleted`, save.
3. Publish `document.generated`.
4. On exception:
   - Non-transient (any exception that is not a transient `DocumentException`, including render errors): `MarkFailed`, save, publish `document.generation.failed`, rethrow.
   - Transient after retries exhausted: rethrow with the row left `Pending`; no failure event.

How the exception outcome interacts with each handler:

| Handler | Exception from the pipeline | Net effect |
|---|---|---|
| `HandlePaymentCompletedHandler` | Caught and logged separately for invoice and receipt (one failing does not block the other) | Message is acked. A `Pending` row from exhausted transient retries is **not** retried automatically. |
| `HandlePaymentRefundedHandler` | Propagates to `ConsumerBase` | Transient or unknown: nack and requeue, then the `Pending` row is retried or a `Failed` row is replayed. Non-transient `DocumentException`: ack. |

Storage errors: `MinioObjectStorageService` wraps every exception into a transient `DocumentStorageException`. Render errors (`DocumentRenderException`, for example an unknown template version) are non-transient by default.

### PDF layout (QuestPDF)

All templates use A4, 40 pt margins, 10 pt default font. Money is formatted as `{amount:N2} {currency}` and dates as `yyyy-MM-dd HH:mm UTC`. No logo or custom font is registered.

| Template | Content |
|---|---|
| `InvoiceDocumentV1` (`invoice-v1`) | Header `Invoice {DocumentNumber}` (18 pt bold); issue date; **Seller** (legal name, `AddressLine1, PostalCode City`, `VAT: {VatId}`); **Customer** (name, email, `AddressLine1, PostalCode City`); 4-column table (Description, Qty, Unit, Total) with line items; right-aligned Subtotal, `Tax ({rate}%)`, bold Total; centered footer `Payment ref: {ProviderPaymentId}` |
| `ReceiptDocumentV1` (`receipt-v1`) | Header `Payment Receipt {ReceiptNumber}`; `Paid at`; seller legal name; `Customer: {name} ({email})`; bold `Amount paid`; `Payment method`; `Order ID`; `Payment ID`; `Provider ref` |
| `CreditNoteDocumentV1` (`credit-note-v1`) | Header `Credit Note {CreditNoteNumber}`; issue date; seller legal name; `Customer: {name} ({email})`; bold `Refund amount`; `Cumulative refunded`; `Remaining balance`; `Original payment`; optional `Refunded items` list (`{description} x{qty}: {lineTotal}`) when line items exist |

`QuestPdfRenderer` dispatches on `templateVersion` plus the request's runtime type, and sets `QuestPDF.Settings.License = LicenseType.Community`. Make sure your use complies with the QuestPDF Community license terms.

---

## 8. Configuration

Settings come from `appsettings.json`, `appsettings.{Environment}.json` and environment variables (ASP.NET Core `__` separator, for example `Database__ConnectionString`). Values below are the committed development defaults; none are production secrets. Replace credentials per environment and never commit real ones.

| Key (env var) | `appsettings.json` (container defaults) | `appsettings.Development.json` (native) | Compose override | Notes |
|---|---|---|---|---|
| `ServiceName` | `pdf-worker` | `pdf-worker` | | OTel resource name (fallback `pdf-worker`) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://jaeger:4317` | `http://localhost:4317` | `http://jaeger:4317` | OTLP gRPC endpoint (traces and logs); code fallback `http://jaeger:4317` |
| `Database:ConnectionString` | `Host=postgres;Port=5433;Database=pdfworker;Username=postgres;Password=postgres` | `Host=localhost;Port=5433;Database=pdfworker;Username=postgres;Password=postgres` | `Host=postgres;Port=5432;Database=pdfworker;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}` | Npgsql connection string. Port `5433` is the host-mapped port; inside the compose network Postgres listens on `5432` |
| `RabbitMQ:Host` | `rabbitmq` | `localhost` | `rabbitmq` | |
| `RabbitMQ:Port` | `5672` | `5672` | `5672` | Class default `5672` |
| `RabbitMQ:Username` | `guest` | `guest` | `${RABBITMQ_USER}` | |
| `RabbitMQ:Password` | `guest` | `guest` | `${RABBITMQ_PASSWORD}` | |
| `RabbitMQ:VirtualHost` | `/` | `/` | `/` | Class default `/` |
| `Minio:Endpoint` | `http://minio:9000` | `http://localhost:9000` | `http://minio:9000` | `http(s)://` prefix is stripped; SSL enabled if value starts with `https` |
| `Minio:AccessKey` | `minioadmin` | `minioadmin` | `${MINIO_ROOT_USER}` | |
| `Minio:SecretKey` | `minioadmin` | `minioadmin` | `${MINIO_ROOT_PASSWORD}` | |
| `Minio:Bucket` | `documents` | `documents` | `documents` | Class default `documents` |
| `Orders:BaseUrl` | `http://orders:8080` | `http://localhost:5136` | `http://orders:8080` | |
| `Seller:LegalName` | `3D Print Shop GmbH` | same | not overridden | Printed on documents |
| `Seller:VatId` | `DE123456789` | same | not overridden | |
| `Seller:Email` | `billing@example.com` | same | not overridden | |
| `Seller:AddressLine1` | `Industriestr. 1` | same | not overridden | |
| `Seller:City` | `Munich` | same | not overridden | |
| `Seller:PostalCode` | `80331` | same | not overridden | |
| `Seller:Country` | `DE` | same | not overridden | |
| `Enrichment:OrdersTimeoutSeconds` | `3` | `3` | not overridden | `HttpClient.Timeout` for Orders calls (class default `3`) |
| `Enrichment:RetryCount` | `2` | `2` | not overridden | Bound but unused (retries are hard-coded to 2 in DI) |
| `Logging:LogLevel:Default` | `Information` | `Information` | | |
| `Logging:LogLevel:Microsoft.AspNetCore` | `Warning` | `Warning` | | |
| `AllowedHosts` | `*` | `*` | | |
| `ASPNETCORE_ENVIRONMENT` | | | `Development` | Controls auto-migration; `Testing` disables health checks and migrations in the host |
| `ASPNETCORE_URLS` | | | `http://+:8080` | |

Compose-level variables (`docker/template.env`, copy to `docker/.env`):

| Variable | Template value | Purpose |
|---|---|---|
| `PDF_WORKER_PORT` | `5140` | Host port mapped to container `8080` |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` | `postgres` / `postgres` | Shared Postgres credentials |
| `POSTGRES_PORT` | `5433` | Host port for Postgres (container `5432`) |
| `RABBITMQ_USER` / `RABBITMQ_PASSWORD` | empty (you must set them) | Broker credentials |
| `RABBITMQ_PORT` / `RABBITMQ_UI_PORT` | `5672` / `15672` | |
| `MINIO_ROOT_USER` / `MINIO_ROOT_PASSWORD` | empty (you must set them) | MinIO credentials |
| `MINIO_PORT` / `MINIO_CONSOLE_PORT` | `9000` / `9001` | |
| `OTEL_UI_PORT` / `OTEL_GRPC_PORT` / `OTEL_HTTP_PORT` | `16686` / `4317` / `4318` | Jaeger UI and OTLP ports |
| `ORDERS_PORT` | `5136` | Orders host port (also the native `Orders:BaseUrl`) |

---

## 9. Observability

| Aspect | Behavior |
|---|---|
| Logging | Providers cleared; console plus OpenTelemetry logging (scopes, formatted message and state values included) exported over OTLP to `OTEL_EXPORTER_OTLP_ENDPOINT` |
| Request logging | `RequestLoggingMiddleware` logs `HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms` |
| MediatR logging | `LoggingBehavior` logs each request's start (including `CorrelationId` and the **full JSON-serialized request**), duration and errors. Event commands carry customer email and amounts, so treat Information-level logs as containing personal data. |
| Correlation id (HTTP) | `CorrelationIdMiddleware` reads `X-Correlation-Id` (or generates a GUID), stores it in `HttpContext.Items` and echoes it on the response header |
| Correlation id (messaging) | Taken from the incoming AMQP `CorrelationId` / `X-Correlation-Id` header, stored in `documents.CorrelationId`, set on published events (property and header) |
| Tracing | OpenTelemetry tracing with ASP.NET Core, HttpClient (exceptions recorded) and EF Core instrumentation; custom `ActivitySource`s `PdfWorker.Messaging` (span `rabbitmq.publish`), `PdfWorker.Storage` (span `minio.upload`) and `PdfWorker.Rendering` (registered but no spans are started); OTLP exporter to Jaeger (`http://jaeger:4317` by default); resource name from `ServiceName`. Trace context is not propagated through RabbitMQ and consumption is not traced. |
| Health | `GET /health` (see section 4) |
| Jaeger UI | `http://localhost:16686` with the compose stack |

---

## 10. Running locally

### Docker Compose (recommended)

The `pdf-worker` image build context is the **repository root** (`context: ..`, `dockerfile: PdfWorker/PdfWorker/Dockerfile`), because the project references `Payments/Contracts`.

```bash
cd docker
cp template.env .env        # then set RABBITMQ_USER/PASSWORD and MINIO_ROOT_USER/PASSWORD
docker compose up -d --build postgres rabbitmq minio orders payments jaeger pdf-worker
```

`pdf-worker` declares `depends_on: postgres, rabbitmq, minio, orders, jaeger` (start order only, no health conditions). Compose runs it with `ASPNETCORE_ENVIRONMENT=Development`, so it migrates its database on start (see Known gaps). The `pdfworker` database is not created by `docker/postgres-init` (which only creates `notifications`); it is created by `Database.Migrate()`.

Check:

```bash
curl http://localhost:5140/health
curl "http://localhost:5140/documents?orderId=<guid>&page=1&pageSize=20"
```

To exercise the full flow, complete a payment through Payments, which publishes `payment.completed`; documents then appear in `GET /documents` and in the MinIO console (`http://localhost:9001`, bucket `documents`).

### Native (`dotnet run`)

Start the infrastructure (at minimum Postgres on `localhost:5433`, RabbitMQ on `5672`, MinIO on `9000`, and optionally Jaeger on `4317` and Orders on `5136`), for example:

```bash
cd docker
docker compose up -d postgres rabbitmq minio jaeger orders
```

Then:

```bash
cd PdfWorker
dotnet build PdfWorker.sln
dotnet run --project PdfWorker          # http://localhost:5140, Development environment
```

`appsettings.Development.json` points to `localhost` services. If your RabbitMQ or MinIO credentials differ from the defaults (`guest/guest`, `minioadmin/minioadmin`), override them with environment variables (for example `RabbitMQ__Username`, `Minio__AccessKey`) or user secrets.

---

## 11. Tests

```bash
cd PdfWorker
dotnet test PdfWorker.sln                          # everything
dotnet test UnitTests/UnitTests.csproj             # unit tests only (no Docker)
dotnet test IntegrationTests/IntegrationTests.csproj
```

| Project | Contents |
|---|---|
| `UnitTests/Application` (13 tests) | `IdempotencyKeyBuilderTests` (3), `InvoiceDocumentRequestValidatorTests` (5, including `ValidateTotalAmount`), `ListDocumentsHandlerTests` (3), `GetDocumentByIdHandlerTests` (2). Moq is used for repositories. |
| `IntegrationTests/Endpoints` (4 tests) | `ListDocumentsTests` (400 without order/payment; list by order) and `GetDocumentByIdTests` (404 for unknown id; get existing). `PdfWorkerApiFactory` (a `WebApplicationFactory<Program>` in the `Testing` environment) starts Testcontainers for PostgreSQL (`postgres:17`), RabbitMQ (`rabbitmq:3-management`) and MinIO (`minio/minio:latest`), seeds a completed document directly through the `DbContext`, and calls `Database.Migrate()`. All tests share the `PdfWorker` collection fixture. |

Docker requirements: the integration tests need a running Docker daemon. They use `[SkippableFact]` with `DockerGate.IsAvailable()` (pings Docker), so they are **skipped** (not failed) when Docker is not available. No tests cover the consumers, the pipeline, the Orders client, the renderer or storage. The 13 unit tests pass in a local run. The integration tests were not executed while writing this document (see Known gaps regarding migrations and `page`/`pageSize`).

---

## 12. Docker image

`PdfWorker/PdfWorker/Dockerfile` is a multi-stage build:

| Stage | Base | Purpose |
|---|---|---|
| `base` | `mcr.microsoft.com/dotnet/aspnet:10.0` | Runtime, `EXPOSE 8080` |
| `build` | `mcr.microsoft.com/dotnet/sdk:10.0` | Copies `PdfWorker.sln` and the project files (including `Payments/Contracts/Contracts.csproj`), `dotnet restore`, copies sources (`PdfWorker/` and `Payments/Contracts/`), `dotnet build -c Release` |
| `publish` | `build` | `dotnet publish -c Release /p:UseAppHost=false` |
| `final` | `base` | Copies the publish output; `ENTRYPOINT ["dotnet", "PdfWorker.dll"]` |

The build context **must be the repository root**, because the Dockerfile uses paths like `PdfWorker/...` and `Payments/Contracts/...`:

```bash
# from the repository root
docker build -f PdfWorker/PdfWorker/Dockerfile -t pdf-worker .
```

`PdfWorker/.dockerignore` excludes `bin/`, `obj/`, `.env` files and similar, but note that it sits in `PdfWorker/` while the context is the repo root, so Docker (which only reads `.dockerignore` at the context root) will not apply it for the repo-root build.

---

## 13. Known gaps and inconsistencies

Observed in the code; none were changed as part of this documentation.

| # | Area | Observation |
|---|---|---|
| 1 | Migrations vs model | The only migration (`InitialCreate`) and model snapshot create table `"Documents"` with integer enums, `text` columns, no indexes and `text` for `RequestContractJson`, whereas `DocumentConfiguration` maps table `documents`, string enums, length limits, `jsonb`, a unique idempotency-key index and four other indexes. The snapshot is out of sync with the model: queries target `documents`, so a database created from the migration would not match, and EF Core's pending-model-changes check on `Migrate()` is expected to flag the difference. A new migration is needed. This was not run end to end. |
| 2 | `page` / `pageSize` | Declared as non-nullable `int` without defaults on `GET /documents`, so they are treated as required query parameters by minimal API binding. The `<= 0` fallbacks in the endpoint cannot apply to missing values, and the integration test `Should_Return_Documents_For_Order` and the root README examples call the endpoint without them. In the `Development` environment the binding error may surface through `ExceptionMiddleware` as `500`. Not verified at runtime. |
| 3 | Pending documents | After exhausted transient retries during `payment.completed` handling, the exception is swallowed, the message is acked and the row stays `Pending` forever (no sweeper or retry job). |
| 4 | Requeue loop | Transient and unknown errors are nacked with `requeue: true` and no delay, retry limit or dead-letter queue. A poison message (for example malformed JSON) is redelivered indefinitely. |
| 5 | Publish failure after upload | `document.generated` is published inside the pipeline's `try` block; if publishing fails after the PDF is stored, the catch block marks the document `Failed` (storage fields remain set) and tries to publish a failure event. There is no outbox. |
| 6 | Failed is final | A `Failed` document is never regenerated on redelivery (replay just returns). Validation failures, including a missing address, are permanent for that idempotency key. |
| 7 | Invoice needs an address | The invoice validator requires `Customer.AddressLine1`, but the mapper only fills it from Orders' shipping address. If Orders returns `404` or no address, the invoice is `Failed` while the receipt is generated. |
| 8 | Orders email | `OrderEnrichmentDto.RecipientEmail` is never populated by `OrdersHttpClient`; the email comes from the payment event or the placeholder `unknown@example.com`. |
| 9 | Orders authentication | `OrdersHttpClient` sends no `Authorization` header. Whether Orders accepts this from this folder alone was not verified. |
| 10 | Tax | `TaxRatePercent` is hard-coded to `19`, and `Tax` is computed as payment amount minus the sum of line totals, not from the rate. `ValidateTotalAmount` is never called by production code. |
| 11 | Credit note lines | Partial-refund credit notes list all order items at their original amounts, not prorated to the refund. `CreditNoteDocumentRequestV1.Reason` is never populated or rendered. |
| 12 | Unused config | `Enrichment:RetryCount` is bound but ignored; `PdfWorker.Rendering` `ActivitySource` has no spans; `Microsoft.Extensions.Http.Polly` is referenced alongside the resilience package. |
| 13 | Base `appsettings.json` | Uses `Port=5433` with host `postgres` (5433 is the host-mapped port; the container port is 5432). Compose overrides it, but running the image without overrides would not connect. |
| 14 | No auth / ownership | The service has no authentication and does not scope reads by `UserId`; no download endpoint exists. |
| 15 | Messages not durable | Published events do not set persistent delivery mode and use no publisher confirms. No consumer of `documents-exchange` exists in the repo yet. |
| 16 | Gateway config | The gateway cluster `pdf-worker-cluster` names its destination `inventory` (copy/paste), and the `Services` section has the key `PdfWOrker` (typo); neither affects routing. |
| 17 | Root README | The PDF Worker section of the root `README.md` references `specs/001-pdf-worker/quickstart.md`, but no `specs/` directory exists in the repository. |
| 18 | `.dockerignore` | `PdfWorker/.dockerignore` is not at the build-context root (repo root), so it has no effect on compose builds. |
| 19 | PII in logs | `LoggingBehavior` logs full request payloads (customer email and address) at Information level. |
