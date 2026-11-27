# ApiGateway

Edge / reverse-proxy service for the 3D-printing e-commerce platform. It is the single HTTP entry point that the storefront talks to: it routes public path prefixes to the internal microservices, adds a correlation id, forwards authenticated-user identity as headers, applies CORS, and exports traces.

The gateway has **no database, no cache, and no message-broker connection**. It is a stateless YARP reverse proxy.

> Read the [Security and known gaps](#security-and-known-gaps) section before relying on the gateway for authentication or rate limiting. As the code stands, neither is enforced on any proxied route.

## Table of contents

1. [Role in the system](#role-in-the-system)
2. [Tech stack](#tech-stack)
3. [Project layout](#project-layout)
4. [Public interface](#public-interface)
5. [Request pipeline](#request-pipeline)
6. [Messaging](#messaging)
7. [Data storage](#data-storage)
8. [Domain model, state and background jobs](#domain-model-state-and-background-jobs)
9. [Configuration](#configuration)
10. [Observability](#observability)
11. [Running locally](#running-locally)
12. [Tests](#tests)
13. [Security and known gaps](#security-and-known-gaps)

---

## Role in the system

```
Storefront (http://localhost:3000)
        |
        v
  ApiGateway  (container :8080, host :${GATEWAY_PORT}, default 5133)
        |  YARP reverse proxy over plain HTTP
        +--> auth               http://auth:8080
        +--> catalog            http://catalog:8080
        +--> payments           http://payments:8080
        +--> orders             http://orders:8080
        +--> inventory          http://inventory:8080
        +--> pdf-worker         http://pdf-worker:8080
        +--> graphrag-api       http://graphrag-api:8100
        +--> notifications-api  http://notifications-api:8200
        |
        +--> jaeger (OTLP gRPC, http://jaeger:4317) for traces
```

Relationships:

- **Downstream services:** the eight HTTP services above (hostnames are the docker-compose service names). The gateway does not talk to PostgreSQL, Redis, RabbitMQ, MinIO, Qdrant, Neo4j or Ollama.
- **Auth service:** issues the JWTs. The gateway validates JWTs with the same issuer, audience and signing secret as `Auth/Auth/appsettings*.json` (issuer and audience values match in the repo).
- **Jaeger:** receives traces over OTLP/gRPC.
- **Storefront:** the only browser origin allowed by CORS by default (`http://localhost:3000`, `http://127.0.0.1:3000`).

## Tech stack

| Item | Value |
|---|---|
| Language / runtime | C#, .NET 10 (`net10.0`), nullable enabled, implicit usings |
| Web SDK | `Microsoft.NET.Sdk.Web` (ASP.NET Core minimal hosting) |
| Reverse proxy | `Yarp.ReverseProxy` 2.3.0 |
| Authentication | `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.6, `System.IdentityModel.Tokens.Jwt` 8.17.0, `Microsoft.IdentityModel.Tokens` 8.17.0 |
| Rate limiting | ASP.NET Core `AddRateLimiter` (plus a legacy `Microsoft.AspNetCore.RateLimiting` 7.0.0-rc.2 package reference) |
| Resilience | `Polly` 8.6.6, `Polly.Extensions.Http` 3.0.0, `Microsoft.Extensions.Http.Polly` 10.0.6 |
| Logging | `Serilog.AspNetCore` 10.0.0 (console sink) |
| Tracing | `OpenTelemetry` 1.15.3, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore` 1.15.2, `OpenTelemetry.Instrumentation.Http` 1.15.1 |
| Other | `Microsoft.AspNetCore.OpenApi` 10.0.3 (referenced, but no OpenAPI endpoint is mapped) |
| Tests | xUnit 2.9.3, FluentAssertions 8.9.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.6, Testcontainers 4.11.0 (+ PostgreSql module), coverlet.collector |
| Container | multi-stage Dockerfile on `mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0` |

## Project layout

The solution is `ApiGateway/ApiGateway.sln` (solution folders `src` and `tests`).

```
ApiGateway/
├── ApiGateway.sln
├── .dockerignore
├── ApiGateway/                       # ASP.NET Core host (the deployable)
│   ├── Program.cs                    # Serilog, services, middleware order, MapReverseProxy, /health
│   ├── appsettings.json              # Jwt, Services, Cors, ReverseProxy (routes + clusters), OTEL endpoint
│   ├── appsettings.Development.json  # identical content to appsettings.json
│   ├── Dockerfile
│   ├── ApiGateway.http               # stale template request (/weatherforecast)
│   ├── Properties/launchSettings.json
│   ├── Configuration/                # CorsOptions, JwtOptions, RateLimitingOptions
│   ├── Extensions/
│   │   ├── ServiceCollectionExtensions.cs   # AddGatewayServices: CORS, JWT, authorization, rate limiter, YARP
│   │   └── ApplicationBuilderExtensions.cs  # MapHealthEndpoints: GET /health
│   └── Middleware/                   # ExceptionMiddleware, CorrelationIdMiddleware, RequestLoggingMiddleware
├── Contracts/                        # shared contracts (no dependencies)
│   ├── Headers/CorrelationHeaders.cs        # const "X-Correlation-Id"
│   └── Responses/ErrorResponse.cs           # record ErrorResponse(string Message)
├── Infrastructure/
│   ├── DependencyInjection/InfrastructureServiceCollectionExtensions.cs  # AddInfrastructure
│   ├── Observability/OpenTelemetrySetup.cs                               # tracing setup
│   ├── Resilience/PollyPolicies.cs                                       # retry + circuit breaker policies
│   └── ReverseProxy/TransformProviders/
│       ├── AddCorrelationIdTransform.cs     # YARP request transform
│       └── AddUserClaimsTransform.cs        # YARP request transform
└── IntegrationTests/                 # xUnit + Testcontainers (+ WebApplicationFactory)
    ├── Clients/GatewayClient.cs
    ├── Common/TestSettings.cs
    ├── Factories/GatewayFactory.cs
    ├── Fixtures/                     # one fixture per container + TestEnvironment orchestrator
    └── Tests/{Auth,GraphRAG,Payments}/
```

Dependencies: `ApiGateway` -> `Contracts`, `Infrastructure`; `IntegrationTests` -> `ApiGateway`.

## Public interface

### Gateway-owned endpoints

| Method | Path | Auth | Response |
|---|---|---|---|
| GET | `/health` | none | `200 OK`, JSON body `"Gateway OK"` (a JSON string) |

This endpoint only checks that the gateway process is up. It does not probe downstream services.

### Route table

All routes and clusters come from the `ReverseProxy` section of `appsettings.json` (loaded with `LoadFromConfig`). Each route matches `{prefix}/{**catch-all}`.

| Route id | Public path | Cluster | Destination address | Auth policy | Rate-limit policy | Transforms (from config) |
|---|---|---|---|---|---|---|
| `auth-public` | `/auth/{**catch-all}` | `auth-cluster` | `http://auth:8080` | none | none | none |
| `catalog-products` | `/products/{**catch-all}` | `catalog-cluster` | `http://catalog:8080` | none | none | none |
| `payments` | `/payments/{**catch-all}` | `payments-cluster` | `http://payments:8080` | none | none | none |
| `orders` | `/orders/{**catch-all}` | `orders-cluster` | `http://orders:8080` | none | none | none |
| `inventory` | `/inventory/{**catch-all}` | `inventory-cluster` | `http://inventory:8080` | none | none | none |
| `pdf-worker-documents` | `/documents/{**catch-all}` | `pdf-worker-cluster` | `http://pdf-worker:8080` | none | none | none |
| `graphrag-search` | `/graphrag/{**catch-all}` | `graphrag-cluster` | `http://graphrag-api:8100` | none | none | `PathRemovePrefix: /graphrag` |
| `notifications` | `/notifications/{**catch-all}` | `notifications-cluster` | `http://notifications-api:8200` | none | none | none |

Notes:

- No route sets `AuthorizationPolicy` or `RateLimiterPolicy`, so every route is anonymous and unthrottled at the gateway (see [Security and known gaps](#security-and-known-gaps)).
- A catch-all segment may be empty, so bare prefixes such as `POST /products` and `POST /payments` (used by the integration tests) are matched as well as deeper paths.
- Except for `/graphrag`, the path is forwarded unchanged: `/auth/login` -> `http://auth:8080/auth/login`.
- `/graphrag/api/search` -> `http://graphrag-api:8100/api/search`.
- Each cluster has a single destination and no load-balancing, health-check, session-affinity or HTTP-client configuration (YARP defaults apply).
- The destination key for `pdf-worker-cluster` is named `inventory` (copy/paste leftover). It has no functional effect.

### Transforms applied to every proxied request

Two global YARP `ITransformProvider`s (registered with `AddTransforms<T>()`) run for every route.

| Transform | Behavior |
|---|---|
| `AddCorrelationIdTransform` | If `HttpContext.Items["X-Correlation-Id"]` is set (done by `CorrelationIdMiddleware`), adds header `X-Correlation-Id` to the downstream request. |
| `AddUserClaimsTransform` | If `HttpContext.User` is authenticated (a valid `Authorization: Bearer` JWT was presented): adds `X-User-Id` from the `NameIdentifier` claim, falling back to `sub`. Adds `X-User-Email` from the `Email` claim, falling back to `email`. Empty values are skipped. |

The inbound `Authorization` header is forwarded unchanged (YARP default), so downstream services can validate the token themselves. Orders, Payments and Notifications read `X-User-Id` / `X-User-Email` (for example for receipt e-mail resolution).

### Error and status behavior

| Situation | Result |
|---|---|
| Downstream responds | The status code and body are passed through unchanged. |
| No route matches | `404` (ASP.NET Core default, empty body). |
| Unhandled exception in the gateway pipeline | `500` with JSON `{"message": "<exception message>"}` (`ErrorResponse`), produced by `ExceptionMiddleware`. |
| Downstream unreachable or times out | Handled by YARP's built-in proxy error handling (typically `502` or `504`). `ExceptionMiddleware` does not intercept these. |
| Invalid token | The request still proceeds anonymously, because the routes do not require authorization. |
| Rate limiter | Nothing is rejected, because no limiter policy is applied (see below). |

Example error payload (`ErrorResponse`):

```json
{ "message": "Object reference not set to an instance of an object." }
```

### Headers

| Header | Direction | Description |
|---|---|---|
| `X-Correlation-Id` | request in | Reused if present, otherwise a new GUID is generated. |
| `X-Correlation-Id` | response out | Always set by `CorrelationIdMiddleware`, and exposed to browsers via CORS. |
| `X-Correlation-Id` | request to downstream | Added by `AddCorrelationIdTransform`. |
| `X-User-Id`, `X-User-Email` | request to downstream | Added by `AddUserClaimsTransform` for valid JWTs. |

### CORS

Policy name `Storefront`, built from the `Cors:AllowedOrigins` array:

- Origins: the configured list. If the list is empty, no origin is allowed (`SetIsOriginAllowed(_ => false)`).
- Any header and any method are allowed. Credentials are not enabled (`AllowCredentials` is not called).
- Exposed response header: `X-Correlation-Id`.

## Request pipeline

Order in `Program.cs`:

1. `ExceptionMiddleware` — catches exceptions and returns a 500 JSON `ErrorResponse`.
2. `CorrelationIdMiddleware` — reads or generates `X-Correlation-Id`, stores it in `HttpContext.Items` and sets the response header.
3. `RequestLoggingMiddleware` — logs `HTTP {Method} {Path}` at Information level.
4. `UseCors("Storefront")`
5. `UseRateLimiter()`
6. `UseAuthentication()` — JWT Bearer is the default scheme. It populates `HttpContext.User` when a valid token is sent.
7. `UseAuthorization()`
8. `MapReverseProxy()` — YARP routes, with the two request transforms above.
9. `MapGet("/health")`

### JWT validation parameters

Validated: issuer, audience, signing key (symmetric, HMAC key derived from the UTF-8 bytes of `Jwt:Secret`) and lifetime. Values come from `Jwt:Issuer` (`auth-service`), `Jwt:Audience` (`ecommerce`) and `Jwt:Secret`. These must match the Auth service.

### Rate-limiter definitions

Two fixed-window limiters are registered in `ServiceCollectionExtensions` (hard-coded, not read from configuration):

| Policy name | Permit limit | Window |
|---|---|---|
| `auth-strict` | 10 | 60 s |
| `global` | 100 | 60 s |

They are **defined but never attached** to any route or endpoint, so they currently have no effect. `Configuration/RateLimitingOptions.cs` (defaults 100 permits / 60 s) is also not used anywhere.

### Resilience

`AddInfrastructure` registers a named `HttpClient` called `proxy` with two Polly handlers:

| Policy | Definition |
|---|---|
| Retry | `HandleTransientHttpError` (5xx, 408, `HttpRequestException`); 3 retries, waits of 200 ms x attempt (200, 400, 600 ms) |
| Circuit breaker | `HandleTransientHttpError`; opens after 5 consecutive failures; stays open for 30 s |

Nothing in the code requests the `proxy` client. YARP creates its own `HttpMessageInvoker` per cluster, so **these policies do not apply to proxied traffic**.

## Messaging

None. The gateway does not publish or consume RabbitMQ messages, and it has no RabbitMQ dependency or configuration.

## Data storage

None. The gateway holds no persistent state: no PostgreSQL, Redis, MinIO, Qdrant or Neo4j. Rate-limiter counters (if used) would be in-process memory. Routes and clusters are read from configuration at startup (`LoadFromConfig`; YARP also reloads when the configuration source changes).

## Domain model, state and background jobs

There is no domain model, state machine or background job. The only business-relevant behavior is the proxy rules described above (routing, identity forwarding, correlation id).

## Configuration

Standard ASP.NET Core configuration: `appsettings.json`, then `appsettings.{Environment}.json`, then environment variables (use `__` as the hierarchy separator, for example `Cors__AllowedOrigins__0`). `appsettings.Development.json` is currently identical to `appsettings.json`.

| Key / env var | Default (in repo) | Meaning |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` (compose, launchSettings) | Selects the `appsettings.{env}.json` overlay. |
| `ASPNETCORE_URLS` | `http://+:8080` (compose) | Listen address in the container. Native `dotnet run` uses `launchSettings.json`. |
| `Jwt:Issuer` | `auth-service` | Expected token `iss`. |
| `Jwt:Audience` | `ecommerce` | Expected token `aud`. |
| `Jwt:Secret` | development placeholder committed in `appsettings*.json` | HMAC signing key. **Required** (the section is dereferenced without a null check, so startup fails if `Jwt` is missing). Must equal the Auth service's secret. Override it in any real deployment and do not commit it. |
| `Cors:AllowedOrigins` | `["http://localhost:3000", "http://127.0.0.1:3000"]` | Origins allowed by the `Storefront` policy. Empty means none allowed. |
| `ReverseProxy:Routes:*` | see [Route table](#route-table) | YARP routes. |
| `ReverseProxy:Clusters:*:Destinations:*:Address` | docker-compose hostnames, see route table | Downstream base URLs. This is what the proxy actually uses. |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://jaeger:4317` (appsettings, compose, and code fallback) | OTLP gRPC endpoint for traces and the log exporter. Read from `IConfiguration`, so it can come from the environment. |
| `Services:Auth`, `Catalog`, `Payments`, `Orders`, `Inventory`, `Graphrag`, `PdfWOrker`, `Notifications` | docker-compose hostnames | **Not read by any code.** The proxy uses the `ReverseProxy:Clusters` addresses. Changing these has no effect. |
| `Services__PdfWorker` (compose only) | `http://pdf-worker:8080` | Compose sets this, but nothing reads it. The appsettings key is spelled `PdfWOrker`, and keys are case-insensitive. |
| `Serilog:*` | not set | `ReadFrom.Configuration` is wired, but there is no `Serilog` section, so defaults apply (plus a console sink). |

Compose and `docker/template.env`:

| Variable | Value in `template.env` | Used for |
|---|---|---|
| `GATEWAY_PORT` | `5133` | Host port mapped to container port 8080. |
| `AUTH_PORT` | `5132` | Auth host port (direct access). |
| `CATALOG_PORT` | `5134` | Catalog host port. |
| `PAYMENTS_PORT` | `5135` | Payments host port. |
| `ORDERS_PORT` | `5136` | Orders host port. |
| `INVENTORY_PORT` | `5137` | Inventory host port. |
| `PDF_WORKER_PORT` | `5140` | PdfWorker host port. |
| `GRAPHRAG_API_PORT` | `8100` | GraphRAG API host and container port. |
| `NOTIFICATIONS_API_PORT` | `8200` | Notifications API host and container port. |
| `OTEL_UI_PORT`, `OTEL_GRPC_PORT`, `OTEL_HTTP_PORT` | `16686`, `4317`, `4318` | Jaeger UI and OTLP ports. |

The gateway's compose service (`api-gateway`) sets `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `Services__*` and `OTEL_EXPORTER_OTLP_ENDPOINT`. It does not set `Jwt__*`, `Cors__*` or any `ReverseProxy__*` values.

## Observability

- **Tracing:** OpenTelemetry tracing with resource service name `gateway`, ASP.NET Core and `HttpClient` instrumentation, and an OTLP exporter to `OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://jaeger:4317`). Jaeger UI is on `http://localhost:${OTEL_UI_PORT}` (default `16686`).
- **Logging:** Serilog (`UseSerilog`, configuration-driven, plus a console sink). `RequestLoggingMiddleware` logs `HTTP {Method} {Path}` for each request. `Program.cs` also calls `builder.Logging.AddOpenTelemetry(...)` with an OTLP log exporter to the same endpoint. Serilog is configured without `writeToProviders`, so it replaces the other logging providers. It is unclear whether the OTel log exporter receives records, and Jaeger all-in-one only stores traces.
- **Correlation id:** `X-Correlation-Id` is generated or propagated and returned to the client and to downstream services. It is not added to the log scope, and the value is not validated.
- **Health:** `GET /health` returns `"Gateway OK"`. There is no readiness check for downstreams, and the compose file defines no healthcheck for the gateway.

## Running locally

### Docker Compose (recommended)

From the repository root:

```powershell
cd docker
Copy-Item template.env .env     # then fill in the empty values (RabbitMQ, MinIO, Neo4j, Stripe, ...)
docker compose up --build
```

The gateway is then available at `http://localhost:5133` (or your `GATEWAY_PORT`):

```powershell
curl http://localhost:5133/health
curl -X POST http://localhost:5133/auth/login -H "Content-Type: application/json" -d '{"email":"user@example.com","password":"..."}'
```

The compose project name is `ecommerce-infra`, so the gateway image is named `ecommerce-infra-api-gateway`. The `depends_on` list (auth, catalog, payments, orders, inventory, graphrag-api, notifications-api, pdf-worker, jaeger) only orders container start-up. It does not wait for readiness.

The Docker build context is `ApiGateway/` and the Dockerfile is `ApiGateway/ApiGateway/Dockerfile`: restore the solution, `dotnet build -c Release`, `dotnet publish -c Release`, then run `dotnet ApiGateway.dll` on the `aspnet:10.0` image (`EXPOSE 8080`).

### Native (`dotnet run`)

Requires the .NET 10 SDK. The default cluster addresses use docker hostnames (`auth`, `catalog`, ...), which do not resolve outside compose. Override them to point at the published host ports:

```powershell
cd ApiGateway/ApiGateway
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:ReverseProxy__Clusters__auth-cluster__Destinations__auth__Address = "http://localhost:5132"
$env:ReverseProxy__Clusters__catalog-cluster__Destinations__catalog__Address = "http://localhost:5134"
$env:ReverseProxy__Clusters__payments-cluster__Destinations__payments__Address = "http://localhost:5135"
$env:ReverseProxy__Clusters__orders-cluster__Destinations__orders__Address = "http://localhost:5136"
$env:ReverseProxy__Clusters__inventory-cluster__Destinations__inventory__Address = "http://localhost:5137"
$env:ReverseProxy__Clusters__pdf-worker-cluster__Destinations__inventory__Address = "http://localhost:5140"
$env:ReverseProxy__Clusters__graphrag-cluster__Destinations__graphrag__Address = "http://localhost:8100"
$env:ReverseProxy__Clusters__notifications-cluster__Destinations__notifications__Address = "http://localhost:8200"
dotnet run --launch-profile http     # http://localhost:5036
```

Launch profiles: `http` -> `http://localhost:5036`; `https` -> `https://localhost:7298` and `http://localhost:5036`. Both use `ASPNETCORE_ENVIRONMENT=Development`.

The Jwt secret in `appsettings.json` must match the Auth service's secret, otherwise tokens will not validate and no `X-User-*` headers are forwarded.

## Tests

There are no unit tests. The only test project is `IntegrationTests` (xUnit, FluentAssertions, Testcontainers).

### What they need

- Docker running locally.
- Pre-built local images (the fixtures use `PullPolicy.Never`): `ecommerce-infra-auth:latest`, `ecommerce-infra-catalog:latest`, `ecommerce-infra-payments:latest`, `ecommerce-infra-graphrag-api:latest`. Build them first with `docker compose build` from `docker/`.
- Public images that are pulled: `postgres` (Testcontainers.PostgreSql), `rabbitmq:management`, `redis:7`, `minio/minio:latest`, `qdrant/qdrant:latest`, `neo4j:latest`, `ollama/ollama:latest`.

`TestEnvironment` (an `IClassFixture`) creates a Docker network and starts Postgres, RabbitMQ, Redis, MinIO, Qdrant, Neo4j, Ollama, Auth, Catalog, Payments and GraphRAG. It is created once per test class, so the whole stack is started repeatedly, which is slow and heavy. The gateway runs in-process through `GatewayFactory` (`WebApplicationFactory<Program>`, environment `Testing`, so only `appsettings.json` applies). The factory overrides the auth, catalog, payments and graphrag cluster addresses with the mapped container ports.

### Run

```powershell
cd ApiGateway
dotnet test IntegrationTests/IntegrationTests.csproj
```

### Test cases

| Class | Test | Checks |
|---|---|---|
| `AuthFlowTests` | `Register_Then_Login_Should_Work` | `POST /auth/register` then `POST /auth/login` succeed. The body contains `accessToken` and `refreshToken`. |
| `CatalogTests` | `Create_Product_Should_Work` | `POST /products` with no token succeeds. The body contains `productId`. |
| `PaymentsTests` | `Create_Payment_Should_Work_Via_Gateway` | `POST /payments` with a request body succeeds. The body contains `paymentId`. |
| `GraphRagRouteTests` | `GraphRag_Health_Through_Gateway_Should_Return_Ok` | `GET /graphrag/health` returns 200 with `status` in the body. |
| `GraphRagRouteTests` | `GraphRag_Search_Through_Gateway_Should_Return_Ok` | `POST /graphrag/api/search` returns 200 or 500. A 200 body must contain `results`. |

Observations about the tests:

- `GraphRagRouteTests` calls `new GatewayFactory(auth, catalog, graphragUrl)`, which passes the GraphRAG URL in the `paymentsUrl` parameter. The `graphrag-cluster` address is therefore not overridden, and the cluster keeps its compose default `http://graphrag-api:8100`.
- `GatewayFactory` imports `Microsoft.VisualStudio.TestPlatform.TestHost`, and `ApiGateway` has no `public partial class Program` or `InternalsVisibleTo`. Which `Program` type the factory binds to is worth verifying.
- The tests that call `/products` and `/payments` with no bearer token succeed, which confirms the gateway does not enforce authentication.
- `TestSettings.BaseGatewayUrl` and `GatewayClient` are unused.
- The Dockerfile copies `IntegrationTests.csproj` and restores the whole solution, so the Testcontainers packages are restored when building the image.

## Security and known gaps

- **No authentication enforced.** JWT Bearer and authorization are registered, but no route has an `AuthorizationPolicy`, and no `RequireAuthorization` is applied. Anonymous callers can reach every downstream route, and an invalid token is simply ignored. Only `Auth` and the gateway reference JWT bearer in the repo, so downstream services do not enforce it either.
- **No rate limiting in effect.** The `auth-strict` and `global` limiters are never attached to routes. The `auth` route is not throttled.
- **Resilience policies unused.** The Polly retry and circuit breaker are bound to a named `proxy` client that YARP never uses.
- **Identity headers are not stripped.** The transforms add `X-User-Id`, `X-User-Email` and `X-Correlation-Id` with `TryAddWithoutValidation`, and YARP copies inbound request headers by default. A client that sends these headers itself may have its values forwarded alongside (or instead of) the gateway's, so downstream services should not treat `X-User-*` as trustworthy unless the gateway removes them. This is not verified by a test.
- **Error leakage.** `ExceptionMiddleware` returns the raw exception message in 500 responses.
- **Secrets in source.** A development JWT signing secret is committed in `appsettings.json` and `appsettings.Development.json`, and compose does not override it. Move it to an environment variable or secret store for anything beyond local development.
- **Config inconsistencies.**
  - The `Services` section is unused, with a `PdfWOrker` spelling.
  - `Services__PdfWorker` is set in compose but unread.
  - The pdf-worker destination is named `inventory`.
  - `appsettings.Development.json` duplicates `appsettings.json`.
  - `RateLimitingOptions` is unused, and the limiter values are hard-coded.
- **Legacy package.** `Microsoft.AspNetCore.RateLimiting` 7.0.0-rc.2 is referenced even though rate limiting is part of the shared framework on .NET 10.
- **Stale or unused files.** `ApiGateway.http` targets `/weatherforecast`, which does not exist. `Microsoft.AspNetCore.OpenApi` is referenced but not mapped.
- **TLS.** Traffic to downstream services is plain HTTP inside the compose network. The container listens on HTTP only (`http://+:8080`).
- **Health.** `/health` is a liveness stub only.
