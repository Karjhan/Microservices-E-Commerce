# Payments Service

The Payments service owns the payment lifecycle for the 3D-printing e-commerce platform. It records a payment for an order, charges it through **Stripe** (PaymentIntents, server-side confirmation), supports full and partial refunds, auto-cancels payments that were never processed, and publishes domain events to **RabbitMQ** so other services can react (Orders, Inventory, PDF Worker, Notifications).

Service name (OpenTelemetry resource): `payments-service`.

## Table of contents

1. [Role in the system](#1-role-in-the-system)
2. [Tech stack](#2-tech-stack)
3. [Architecture and project layout](#3-architecture-and-project-layout)
4. [HTTP API](#4-http-api)
5. [Messaging (RabbitMQ)](#5-messaging-rabbitmq)
6. [Data storage](#6-data-storage)
7. [Domain model, state machine, Stripe flow, background job](#7-domain-model-state-machine-stripe-flow-background-job)
8. [Configuration](#8-configuration)
9. [Observability](#9-observability)
10. [Running locally](#10-running-locally)
11. [Tests](#11-tests)
12. [Security notes, known gaps and inconsistencies](#12-security-notes-known-gaps-and-inconsistencies)

---

## 1. Role in the system

```
Storefront ──► API Gateway (/payments/**) ──► Payments ──► Stripe (PaymentIntents / Refunds)
                                                 │
                                                 ├──► PostgreSQL  (database "payments")
                                                 ├──► Redis       (read cache)
                                                 └──► RabbitMQ    (exchange "payments-exchange")
                                                          │
                       ┌────────────┬─────────────┬───────┴──────┬──────────────┐
                       ▼            ▼             ▼              ▼
                    Orders      Inventory      PdfWorker     Notifications
```

* **Inbound HTTP**: the API Gateway routes `/payments/{**catch-all}` to `http://payments:8080` (`ApiGateway/ApiGateway/appsettings*.json`, cluster `payments-cluster`). The service itself performs **no authentication or authorization** (see [section 12](#12-security-notes-known-gaps-and-inconsistencies)).
* **Outbound**: Stripe (HTTPS), PostgreSQL, Redis, RabbitMQ, OTLP collector (Jaeger).
* **Messaging**: Payments only **publishes** events. It has no RabbitMQ consumers. Consumers found elsewhere in the repository (by exchange and routing-key usage):

| Routing key | Consumers found in repository |
|---|---|
| `payment.created` | none found |
| `payment.completed` | Orders, Inventory, PdfWorker, Notifications |
| `payment.failed` | Orders, Inventory, Notifications |
| `payment.cancelled` | Inventory, Notifications |
| `payment.refunded` | PdfWorker, Notifications |

---

## 2. Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10 (`net10.0` in every project), C# with nullable reference types and implicit usings |
| Web | ASP.NET Core Minimal APIs (`Microsoft.NET.Sdk.Web`), `Microsoft.AspNetCore.OpenApi` 10.0.3 (package referenced; no OpenAPI endpoint is mapped in `Program.cs`) |
| CQRS / mediator | MediatR 13.0.0 (with a `LoggingBehavior<,>` pipeline behavior) |
| Persistence | EF Core 10.0.6, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1 |
| Cache | `StackExchange.Redis` 2.12.14 |
| Messaging | `RabbitMQ.Client` 7.2.1 |
| Payment provider | `Stripe.net` 46.2.0 |
| Health checks | `AspNetCore.HealthChecks.NpgSql` / `.Rabbitmq` / `.Redis` 9.0.0 (`AspNetCore.HealthChecks.UI.Client` 9.0.0 is referenced but not used) |
| Observability | OpenTelemetry 1.15.x: `Extensions.Hosting`, OTLP exporter, instrumentation for ASP.NET Core, `HttpClient`, EF Core, StackExchange.Redis |
| Unit tests | xUnit 2.9.3, Moq 4.20.72, FluentAssertions 8.9.0, `Microsoft.EntityFrameworkCore.Sqlite` 10.0.6 |
| Integration tests | xUnit, `Microsoft.AspNetCore.Mvc.Testing` 10.0.6, Testcontainers 4.11.0 (PostgreSQL, Redis, RabbitMQ) |
| Container | `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime), `dotnet/sdk:10.0` (build) |

---

## 3. Architecture and project layout

Clean Architecture with CQRS. Dependencies point inward: `Payments` (API) -> `Application` + `Infrastructure` -> `Domain` / `Contracts`.

```
Payments/
├── Payments.sln
├── Payments/                       API host (Microsoft.NET.Sdk.Web)
│   ├── Program.cs                  DI, middleware, health checks, route groups
│   ├── RecipientEmailResolver.cs   body value -> X-User-Email header fallback
│   ├── Endpoints/                  One static class per endpoint (+ request records)
│   ├── Middleware/                 CorrelationId, RequestLogging, Exception
│   ├── appsettings.json / appsettings.Development.json
│   ├── Payments.http               Sample requests (port 5072)
│   ├── Properties/launchSettings.json
│   └── Dockerfile
├── Application/                    Use cases (no framework dependencies except MediatR)
│   ├── Abstractions/               ICacheService, IEventPublisher, IPaymentProvider, IPaymentRepository (+ PaymentFilter)
│   ├── Common/LoggingBehavior.cs   MediatR pipeline: logs request payload + elapsed time
│   ├── DTOs/                       PaymentDto, PaymentTransactionDto, PaginatedListDto<T>
│   ├── DependencyInjection/        AddApplication()
│   └── Features/Payments/          CreatePayment, ProcessPayment, RefundPayment,
│                                   GetPaymentById, GetPaymentByOrderId, GetPayments
├── Domain/
│   ├── Entities/                   Payment (aggregate root), PaymentTransaction
│   ├── Enums/                      PaymentStatus, PaymentMethod, TransactionType
│   └── Exceptions/                 PaymentException hierarchy (with string codes)
├── Contracts/
│   ├── Events/                     PaymentCreated, PaymentCompleted, PaymentFailed,
│   │                               PaymentRefunded, PaymentCancelled
│   └── ActivitySources.cs          "Payments.Messaging" ActivitySource
├── Infrastructure/
│   ├── BackgroundJobs/             ExpiredPaymentJob
│   ├── Caching/                    RedisCacheService
│   ├── Configuration/              DatabaseOptions, RabbitMqOptions, RedisOptions, StripeOptions
│   ├── DependencyInjection/        AddInfrastructure()
│   ├── Messaging/                  RabbitMqPublisher
│   ├── Observability/              OpenTelemetrySetup (tracing)
│   ├── Payments/                   StripePaymentProvider
│   ├── Payments/Migrations/        20260608155217_InitialCreateFirst1 (adds RecipientEmail)
│   └── Persistence/                PaymentsDbContext, Configurations/, Repositories/,
│                                   Migrations/ (initial migration + model snapshot)
├── UnitTests/                      Domain, handler, repository, resolver tests
└── IntegrationTests/               HTTP tests against Testcontainers
```

Key patterns:

* Each endpoint maps an HTTP request to a MediatR command/query; handlers depend only on abstractions in `Application/Abstractions`.
* `Payment` is a rich aggregate: every state change goes through a guarded method that throws a `PaymentException` subtype.
* `IPaymentProvider` isolates Stripe; `StripePaymentProvider` is the only implementation.
* Middleware order (in `Program.cs`): `CorrelationIdMiddleware` -> `RequestLoggingMiddleware` -> `ExceptionMiddleware`.

---

## 4. HTTP API

All endpoints are under the `/payments` route group (plus `/health`). **None require authentication or roles**; the service does not configure authentication/authorization middleware.

JSON conventions (HTTP):

* Property names are camelCase (ASP.NET Core web defaults).
* Enums are serialized as **strings** in responses (`JsonStringEnumConverter` is registered). Request bodies and query parameters accept the enum name (e.g. `"CreditCard"`) or its integer value (the integration tests send `Method = 0`, `status=2`).
* Timestamps are UTC ISO-8601; money values are JSON numbers.
* Every response carries an `X-Correlation-Id` header (echoes the request header or is generated).

### Endpoint summary

| Method | Path | Auth | Success | Description |
|---|---|---|---|---|
| `POST` | `/payments` | none | `201 Created` | Create a payment in `Pending` state (no Stripe call) |
| `POST` | `/payments/{id}/process` | none | `200 OK` | Charge via Stripe |
| `POST` | `/payments/{id}/refund` | none | `200 OK` | Full or partial Stripe refund |
| `GET` | `/payments/{id}` | none | `200 OK` | Get payment (with transactions) by id |
| `GET` | `/payments/by-order/{orderId}` | none | `200 OK` | Get payment by order id |
| `GET` | `/payments` | none | `200 OK` | Paginated, filterable list |
| `GET` | `/health` | none | `200` / `503` | Health report (not mapped in the `Testing` environment) |

There is **no** `/payments/webhook` endpoint and no Stripe webhook handling in this codebase.

### Error format

`ExceptionMiddleware` converts exceptions to a JSON body (and logs them at error level):

```json
{
  "error": "Payment 7a0e7ea8-9744-45aa-a979-5d9c56753b1b not found",
  "code": "payment.not_found",
  "status": 404,
  "traceId": "0HNG3ABCDEF:00000001"
}
```

`code` is `null` for non-domain exceptions.

| Exception | HTTP status | `code` |
|---|---|---|
| `PaymentNotFoundException` | 404 | `payment.not_found` |
| `DuplicatePaymentException` | 409 | `payment.duplicate` |
| `InvalidPaymentStateException` | 400 | `payment.invalid_state` |
| `InvalidRefundException` | 400 | `payment.invalid_refund` |
| `KeyNotFoundException` (by-order lookup miss) | 404 | `null` |
| `ArgumentException` | 400 | `null` |
| any other exception | 500 | `null` (exception message is returned in `error`) |

### `POST /payments`

Creates a payment in `Pending` status with `expiresAt = now + 30 min` and publishes `payment.created`.

Request (`CreatePaymentRequest`):

| Field | Type | Required | Notes |
|---|---|---|---|
| `orderId` | Guid | yes | Order the payment belongs to |
| `userId` | Guid | yes | Paying user |
| `amount` | decimal | yes | Stored as `numeric(18,2)` |
| `currency` | string | yes | Max 10 chars in DB; lower-cased when sent to Stripe |
| `method` | `PaymentMethod` | yes | `CreditCard`, `DebitCard`, `BankTransfer`, `Wallet` (or 0-3) |
| `idempotencyKey` | string | yes | Max 200 chars, unique |
| `paymentToken` | string | yes (see note) | Stripe PaymentMethod id (`pm_...`), max 300 chars; required to later process the payment |
| `recipientEmail` | string | no | Overrides the `X-User-Email` header; see [recipient email](#recipient-email-resolution) |

```json
{
  "orderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "userId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "amount": 49.99,
  "currency": "EUR",
  "method": "CreditCard",
  "idempotencyKey": "order-d290f1ee-payment-001",
  "paymentToken": "pm_card_visa",
  "recipientEmail": "customer@example.com"
}
```

Response `201 Created`, `Location: /payments/{paymentId}`:

```json
{ "paymentId": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b" }
```

Status codes: `201`, `409` (idempotency key already used, `payment.duplicate`), `500` for unexpected failures. The service performs no explicit field validation (see [section 12](#12-security-notes-known-gaps-and-inconsistencies)).

### `POST /payments/{id}/process`

No request body. Loads the payment, charges Stripe and returns:

```json
{ "message": "Payment processed" }
```

**A declined / failed charge still returns `200 OK`**: the payment is moved to `Failed` and a `payment.failed` event is published. Inspect the payment (`status`, `failureReason`) or the event to learn the outcome.

| Status | Condition |
|---|---|
| `200` | Charge attempted (payment is now `Completed` or `Failed`) |
| `404` | Payment not found (`payment.not_found`) |
| `400` | Payment expired (it is cancelled, `payment.cancelled` is published, then `payment.invalid_state` "Payment has expired and was cancelled."), has no token, or is not `Pending` (`payment.invalid_state`) |

### `POST /payments/{id}/refund`

Optional JSON body (`RefundPaymentRequest`):

| Field | Type | Notes |
|---|---|---|
| `amount` | decimal? | Omit / `null` / empty body = refund the full remaining amount (`amount - refundedAmount`) |

```json
{ "amount": 10.00 }
```

Response `200 OK`:

```json
{ "message": "Payment refunded" }
```

| Status | Condition |
|---|---|
| `200` | Refund succeeded at Stripe and was recorded |
| `404` | Payment not found |
| `400` | Payment not `Completed`/`PartiallyRefunded`; amount `<= 0`; amount exceeds the remaining refundable amount; or Stripe refund failed (`payment.invalid_refund`) |

### `GET /payments/{id}`

`id` must be a GUID (route constraint `:guid`). Returns a `PaymentDto`:

```json
{
  "id": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b",
  "orderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "userId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "amount": 49.99,
  "currency": "EUR",
  "status": "PartiallyRefunded",
  "method": "CreditCard",
  "idempotencyKey": "order-d290f1ee-payment-001",
  "providerPaymentId": "pi_3Q1abcDEFghiJKLm0xyz1234",
  "failureReason": null,
  "refundedAmount": 10.00,
  "transactions": [
    {
      "id": "0f3c1c0e-5d0a-4a52-9c1e-3f6a1b2c4d5e",
      "type": "Charge",
      "amount": 49.99,
      "providerTransactionId": "pi_3Q1abcDEFghiJKLm0xyz1234",
      "success": true,
      "failureReason": null,
      "createdAt": "2026-06-08T15:52:17.123Z"
    },
    {
      "id": "5b8a7e11-2c34-4d6f-8a90-1b2c3d4e5f60",
      "type": "Refund",
      "amount": 10.00,
      "providerTransactionId": "re_3Q1abcDEFghiJKLm0xyz5678",
      "success": true,
      "failureReason": null,
      "createdAt": "2026-06-08T16:10:02.456Z"
    }
  ],
  "createdAt": "2026-06-08T15:52:10.000Z",
  "updatedAt": "2026-06-08T16:10:02.456Z",
  "completedAt": "2026-06-08T15:52:17.123Z",
  "expiresAt": "2026-06-08T16:22:10.000Z"
}
```

`PaymentDto` does not expose `paymentToken` or `recipientEmail`. Status codes: `200`, `404` (`payment.not_found`). Response is cached for 5 minutes in Redis.

### `GET /payments/by-order/{orderId}`

`orderId` must be a GUID. Returns the same `PaymentDto`. `404` with `code: null` if no payment exists for the order. If several payments exist for one order (there is no unique constraint on `OrderId`), the query uses `FirstOrDefault` without ordering, so the result is not deterministic. Cached 5 minutes.

### `GET /payments`

Returns a `PaginatedListDto<PaymentDto>` ordered by `createdAt` descending.

| Query param | Type | Description |
|---|---|---|
| `userId` | Guid? | Filter by user |
| `orderId` | Guid? | Filter by order |
| `status` | `PaymentStatus?` | `Pending`, `Processing`, `Completed`, `Failed`, `Refunded`, `PartiallyRefunded`, `Cancelled` (or 0-6) |
| `method` | `PaymentMethod?` | `CreditCard`, `DebitCard`, `BankTransfer`, `Wallet` (or 0-3) |
| `fromDate` | DateTime? | `createdAt >= fromDate` |
| `toDate` | DateTime? | `createdAt <= toDate` |
| `page` | int | 1-based. `0` is mapped to `1` |
| `pageSize` | int | `0` is mapped to `20`. No maximum is enforced |

Note: `page` and `pageSize` are declared as non-nullable `int` parameters in `GetPaymentsEndpoint`. In ASP.NET Core minimal APIs that makes them required query parameters, so omitting them is expected to return `400` (not verified at runtime); the `0 -> default` fallbacks only apply when `0` is passed explicitly. Always send both (the `.http` file and tests do).

```json
{
  "items": [ { "...": "PaymentDto as shown above" } ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 42,
  "totalPages": 5
}
```

Cached 2 minutes per distinct filter (key = SHA-256 of the serialized filter).

### `GET /health`

Custom JSON writer; registered only when the environment is not `Testing`. Checks: `postgres` (always), `redis` (only if `Redis:ConnectionString` is set), `rabbitmq` (always).

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgres", "status": "Healthy", "error": null },
    { "name": "redis", "status": "Healthy", "error": null },
    { "name": "rabbitmq", "status": "Healthy", "error": null }
  ]
}
```

ASP.NET Core's default status mapping applies (`Unhealthy` -> HTTP 503). Note: `/health` is under the service root, so through the gateway it is not reachable via the `/payments/**` route.

### Recipient email resolution

`RecipientEmailResolver.Resolve(httpContext, fromRequest)` is used by `POST /payments` only:

1. `recipientEmail` from the request body, if not blank (trimmed);
2. otherwise the `X-User-Email` request header, if not blank (trimmed);
3. otherwise `null`.

The service does **not** read JWT claims itself; it relies on the `X-User-Email` header being supplied by the caller (the API Gateway source contains `AddUserClaimsTransform.cs`, which references this header name, but its behaviour was not part of this review). The resolved address is stored in `Payments.RecipientEmail` and copied into the `RecipientEmail` field of the `payment.completed`, `payment.failed`, `payment.cancelled` and `payment.refunded` events. `payment.created` does not carry it. Notifications skips events without a recipient email.

---

## 5. Messaging (RabbitMQ)

### Topology

| Item | Value |
|---|---|
| Exchange | `payments-exchange` |
| Type | `topic` |
| Durable | `true` (declared with `ExchangeDeclareAsync` on every publish; idempotent) |
| Queues | **None declared by Payments.** Consumers declare and bind their own queues |
| Routing keys published | `payment.created`, `payment.completed`, `payment.failed`, `payment.refunded`, `payment.cancelled` |
| Connection | Single shared `IConnection` (singleton) built from `RabbitMQ:*` settings; one channel is opened per publish |

Publisher behavior (`Infrastructure/Messaging/RabbitMqPublisher.cs`):

* Body = `JsonSerializer.Serialize(message)` with **default options**, UTF-8. Therefore event property names are **PascalCase** (`PaymentId`, `RecipientEmail`, ...), enums are already strings in the contracts, `Guid` values are strings, `DateTime` values are ISO-8601 UTC, decimals are JSON numbers.
* `BasicPublishAsync(mandatory: false)` with **no message properties**: no `persistent` delivery mode, no content type, no correlation-id/header propagation. Messages are therefore transient even though the exchange is durable.
* A tracing activity `rabbitmq.publish` (source `Payments.Messaging`) is created with tags `messaging.system=rabbitmq` and `messaging.destination=<routingKey>`.
* Events are published **after** the database commit, directly from the request/handler (no outbox). If publishing throws, the DB change is already committed.

### Events

All contracts live in `Contracts/Events`. `RecipientEmail` is `string?` (may be `null`).

#### `payment.created` - `PaymentCreated`

Published by `CreatePaymentHandler` after the payment row is saved.

| Field | Type |
|---|---|
| `PaymentId` | Guid |
| `OrderId` | Guid |
| `UserId` | Guid |
| `Amount` | decimal |
| `Currency` | string |
| `Method` | string (`PaymentMethod` name) |
| `Status` | string (`Pending`) |
| `IdempotencyKey` | string |
| `CreatedAt` | DateTime (UTC) |
| `ExpiresAt` | DateTime (UTC) |

```json
{
  "PaymentId": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b",
  "OrderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "UserId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "Amount": 49.99,
  "Currency": "EUR",
  "Method": "CreditCard",
  "Status": "Pending",
  "IdempotencyKey": "order-d290f1ee-payment-001",
  "CreatedAt": "2026-06-08T15:52:10.000Z",
  "ExpiresAt": "2026-06-08T16:22:10.000Z"
}
```

#### `payment.completed` - `PaymentCompleted`

Published by `ProcessPaymentHandler` when Stripe returns `succeeded`.

| Field | Type |
|---|---|
| `PaymentId`, `OrderId`, `UserId` | Guid |
| `Amount` | decimal |
| `Currency` | string |
| `ProviderPaymentId` | string (Stripe PaymentIntent id) |
| `CompletedAt` | DateTime (UTC) |
| `RecipientEmail` | string? |

```json
{
  "PaymentId": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b",
  "OrderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "UserId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "Amount": 49.99,
  "Currency": "EUR",
  "ProviderPaymentId": "pi_3Q1abcDEFghiJKLm0xyz1234",
  "CompletedAt": "2026-06-08T15:52:17.123Z",
  "RecipientEmail": "customer@example.com"
}
```

#### `payment.failed` - `PaymentFailed`

Published by `ProcessPaymentHandler` when the charge is not successful (decline, Stripe error, or any non-`succeeded` PaymentIntent status including `requires_action`).

| Field | Type |
|---|---|
| `PaymentId`, `OrderId`, `UserId` | Guid |
| `Amount` | decimal |
| `Currency` | string |
| `Reason` | string (provider failure reason) |
| `FailedAt` | DateTime (UTC) |
| `RecipientEmail` | string? |

```json
{
  "PaymentId": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b",
  "OrderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "UserId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "Amount": 49.99,
  "Currency": "EUR",
  "Reason": "Your card was declined.",
  "FailedAt": "2026-06-08T15:52:17.123Z",
  "RecipientEmail": "customer@example.com"
}
```

#### `payment.refunded` - `PaymentRefunded`

Published by `RefundPaymentHandler` after a successful Stripe refund (every refund, partial or full).

| Field | Type |
|---|---|
| `PaymentId`, `OrderId`, `UserId` | Guid |
| `RefundAmount` | decimal (this refund) |
| `TotalRefunded` | decimal (cumulative) |
| `OriginalAmount` | decimal |
| `Currency` | string |
| `IsFullRefund` | bool (`true` when status became `Refunded`) |
| `RefundedAt` | DateTime (UTC) |
| `RecipientEmail` | string? |

```json
{
  "PaymentId": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b",
  "OrderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "UserId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "RefundAmount": 10.00,
  "TotalRefunded": 10.00,
  "OriginalAmount": 49.99,
  "Currency": "EUR",
  "IsFullRefund": false,
  "RefundedAt": "2026-06-08T16:10:02.456Z",
  "RecipientEmail": "customer@example.com"
}
```

#### `payment.cancelled` - `PaymentCancelled`

Published when a `Pending` payment is cancelled because it expired: either by `ExpiredPaymentJob` (`Reason` = `"Payment expired (auto-cancelled)"`) or by `ProcessPaymentHandler` when `/process` is called on an expired payment (`Reason` = `"Payment expired before processing"`). There is no manual-cancel endpoint.

| Field | Type |
|---|---|
| `PaymentId`, `OrderId`, `UserId` | Guid |
| `Reason` | string |
| `CancelledAt` | DateTime (UTC) |
| `RecipientEmail` | string? |

```json
{
  "PaymentId": "7a0e7ea8-9744-45aa-a979-5d9c56753b1b",
  "OrderId": "d290f1ee-6c54-4b01-90e6-d701748f0851",
  "UserId": "a290f1ee-6c54-4b01-90e6-d701748f0852",
  "Reason": "Payment expired (auto-cancelled)",
  "CancelledAt": "2026-06-08T16:23:00.000Z",
  "RecipientEmail": "customer@example.com"
}
```

### Consumers

Payments has no consumers. Downstream consumers (Orders, Inventory, PdfWorker, Notifications) live in their own services and bind their own queues to `payments-exchange`. Because Payments serializes with default (PascalCase) property names, consumers must deserialize case-insensitively; the Notifications handlers refer to `recipientEmail` in their skip messages, so this is worth verifying on the consumer side.

---

## 6. Data storage

### PostgreSQL

* Database name: `payments` (the connection string default is `Database=payments`; the docker `postgres` container's own default database `POSTGRES_DB=ecommerce` is not used by this service, and `docker/postgres-init` only creates `notifications`).
* The database itself is created by EF Core `Database.Migrate()` (Development environment) if it does not exist.
* Context: `PaymentsDbContext`; configurations are applied from the assembly (`PaymentConfiguration`, `PaymentTransactionConfiguration`).
* Table and column names are the EF defaults (PascalCase, quoted identifiers). Enums are stored as **integers** (no string conversion is configured).

#### Table `Payments`

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK (`PK_Payments`), generated in code (`Guid.NewGuid()`) |
| `OrderId` | `uuid` | no | Index `IX_Payments_OrderId` (non-unique) |
| `UserId` | `uuid` | no | Index `IX_Payments_UserId` |
| `Amount` | `numeric(18,2)` | no | |
| `Currency` | `varchar(10)` | no | Stored as supplied (default in entity: `EUR`) |
| `Status` | `integer` | no | `PaymentStatus`: 0 Pending, 1 Processing, 2 Completed, 3 Failed, 4 Refunded, 5 PartiallyRefunded, 6 Cancelled. Index `IX_Payments_Status` |
| `Method` | `integer` | no | `PaymentMethod`: 0 CreditCard, 1 DebitCard, 2 BankTransfer, 3 Wallet |
| `IdempotencyKey` | `varchar(200)` | no | **Unique** index `IX_Payments_IdempotencyKey` |
| `PaymentToken` | `varchar(300)` | yes | Stripe PaymentMethod id (`pm_...`) as received; not cleared after use |
| `ProviderPaymentId` | `varchar(200)` | yes | Stripe PaymentIntent id (`pi_...`), set on success |
| `FailureReason` | `varchar(1000)` | yes | Set on failure |
| `RefundedAmount` | `numeric(18,2)` | no | Running total, starts at 0 |
| `CreatedAt` | `timestamptz` | no | UTC. Index `IX_Payments_CreatedAt` |
| `UpdatedAt` | `timestamptz` | yes | Set on each state change |
| `CompletedAt` | `timestamptz` | yes | Set on completion |
| `ExpiresAt` | `timestamptz` | no | `CreatedAt + 30 minutes` |
| `RecipientEmail` | `varchar(329)` | yes | Added by migration `InitialCreateFirst1` |

#### Table `PaymentTransactions`

Append-only log of provider interactions.

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK (`PK_PaymentTransactions`) |
| `PaymentId` | `uuid` | no | FK `FK_PaymentTransactions_Payments_PaymentId` -> `Payments(Id)` `ON DELETE CASCADE`; index `IX_PaymentTransactions_PaymentId` |
| `Type` | `integer` | no | `TransactionType`: 0 Charge, 1 Refund |
| `Amount` | `numeric(18,2)` | no | Charge amount or refund amount |
| `ProviderTransactionId` | `varchar(200)` | yes | PaymentIntent id (charge) or Stripe refund id `re_...` (refund); `null` on failed charges |
| `Success` | `boolean` | no | |
| `FailureReason` | `varchar(1000)` | yes | |
| `CreatedAt` | `timestamptz` | no | UTC |

What is written when:

| Event | `Payments` | `PaymentTransactions` |
|---|---|---|
| Create | New row, `Status=0` | - |
| Process, charge ok | `Status=2`, `ProviderPaymentId`, `CompletedAt`, `UpdatedAt` | Charge row, `Success=true` |
| Process, charge failed | `Status=3`, `FailureReason`, `UpdatedAt` | Charge row, `Success=false`, `FailureReason` |
| Refund ok | `RefundedAmount += amount`, `Status` = 5 or 4 | Refund row, `Success=true` (failed refunds are not recorded) |
| Expiry | `Status=6`, `UpdatedAt` | - |

#### Migrations

| Migration | Location / namespace | Content |
|---|---|---|
| `20260512184938_InitialCreateFirst` | `Infrastructure/Persistence/Migrations` (`Infrastructure.Persistence.Migrations`) | Creates both tables, FK and all indexes |
| `20260608155217_InitialCreateFirst1` | `Infrastructure/Payments/Migrations` (`Infrastructure.Payments.Migrations`) | Adds `Payments.RecipientEmail varchar(329) NULL` |
| `PaymentsDbContextModelSnapshot` | `Infrastructure/Persistence/Migrations` | Current model (includes `RecipientEmail`) |

### Redis

Used purely as a read cache; the service works without cached values (cache misses fall through to PostgreSQL). Values are stored as **JSON strings** (`StringSet`) produced with default `System.Text.Json` options, so property names are PascalCase (e.g. `Id`, `OrderId`, `Transactions`) and enums appear as the string values held in the DTO.

| Key pattern | Value | TTL | Written by | Invalidated by |
|---|---|---|---|---|
| `payment:{paymentId}` | `PaymentDto` JSON | 5 min | `GetPaymentByIdHandler` | process (success and failure), refund, expiry job |
| `payment:order:{orderId}` | `PaymentDto` JSON | 5 min | `GetPaymentByOrderIdHandler` | process (success only), refund, expiry job |
| `payments:{SHA256-hex of filter JSON}` | `PaginatedListDto<PaymentDto>` JSON | 2 min | `GetPaymentsHandler` | create, process (any outcome), refund, expiry job (prefix `payments:*`) |

* `RedisCacheService.RemoveByPrefixAsync` enumerates keys with `server.Keys(pattern: prefix*)` on the first endpoint and deletes them one by one.
* `RedisOptions.DefaultTtlSeconds` (default 300) is used only if a caller passes no TTL; all current callers pass an explicit TTL.
* The connection is created with `AbortOnConnectFail=false` and `ConnectRetry=10`.
* `ICacheService` is registered **only if `Redis:ConnectionString` is non-empty**; the handlers require it, so effectively Redis is mandatory.

---

## 7. Domain model, state machine, Stripe flow, background job

### Payment state machine

```
                   ┌─────────┐   MarkProcessing()   ┌────────────┐  MarkCompleted(pi)  ┌───────────┐
   new Payment ──► │ Pending │ ───────────────────► │ Processing │ ──────────────────► │ Completed │
                   └────┬────┘                      └─────┬──────┘                     └─────┬─────┘
                        │ MarkCancelled()                 │ MarkFailed(reason)               │ ApplyRefund(partial)
                        ▼                                 ▼                                  ▼
                   ┌───────────┐                     ┌────────┐                  ┌───────────────────┐
                   │ Cancelled │                     │ Failed │                  │ PartiallyRefunded │◄─┐ ApplyRefund
                   └───────────┘                     └────────┘                  └─────────┬─────────┘──┘ (partial)
                                                                                           │ ApplyRefund (remainder)
        ApplyRefund(full amount) from Completed ───────────────────────────────►  ┌────────▼───┐
                                                                                   │  Refunded  │
                                                                                   └────────────┘
```

Rules enforced in `Payment`:

| Method | Allowed from | Throws |
|---|---|---|
| `MarkProcessing()` | `Pending` | `InvalidPaymentStateException` |
| `MarkCompleted(providerPaymentId)` | `Processing` | `InvalidPaymentStateException` |
| `MarkFailed(reason)` | `Processing` | `InvalidPaymentStateException` |
| `MarkCancelled()` | `Pending` | `InvalidPaymentStateException` |
| `ApplyRefund(amount)` | `Completed`, `PartiallyRefunded` | `InvalidRefundException` (wrong state, `amount <= 0`, or `RefundedAmount + amount > Amount`) |

`IsExpired` = `Status == Pending && UtcNow >= ExpiresAt`. `Failed`, `Cancelled` and `Refunded` are terminal; a failed payment cannot be retried - create a new payment with a new idempotency key.

### Create flow

1. `GetByIdempotencyKeyAsync`; if found -> `DuplicatePaymentException` (409).
2. `new Payment(...)` (`Pending`, `ExpiresAt = now + 30 min`), `SetRecipientEmail`.
3. Insert + `SaveChangesAsync`; remove cache prefix `payments:`.
4. Publish `payment.created`.

### Process flow (`ProcessPaymentHandler`)

1. Load payment (404 if missing).
2. If expired -> `MarkCancelled`, save, publish `payment.cancelled`, throw `InvalidPaymentStateException` (400).
3. If `PaymentToken` empty -> `InvalidPaymentStateException`.
4. `MarkProcessing()` (in memory), then `IPaymentProvider.ChargeAsync(token, amount, currency, idempotencyKey)`.
5. Add a `Charge` `PaymentTransaction` (success or failure).
6. Success -> `MarkCompleted`, save, invalidate caches, publish `payment.completed`. Failure -> `MarkFailed`, save, invalidate caches, publish `payment.failed`.

The `Processing` status is never persisted on its own: it is set and replaced with `Completed`/`Failed` within the same unit of work before `SaveChangesAsync`.

### Refund flow (`RefundPaymentHandler`)

1. Load payment; `refundAmount = request.Amount ?? (Amount - RefundedAmount)`.
2. `ApplyRefund(refundAmount)` (domain validation, status becomes `PartiallyRefunded` or `Refunded`).
3. `IPaymentProvider.RefundAsync(ProviderPaymentId, refundAmount)`; on failure throw `InvalidRefundException` before `SaveChangesAsync` (nothing is persisted).
4. Add a `Refund` transaction, save, invalidate caches, publish `payment.refunded`.

### Stripe integration (`StripePaymentProvider`)

* Client: a singleton `StripeClient(Stripe:SecretKey)`.
* **Charge**: `PaymentIntentService.CreateAsync` with `Amount = round(amount * 100)` (minor units), `Currency = currency.ToLowerInvariant()`, `PaymentMethod = paymentToken`, `Confirm = true`, `ReturnUrl = "https://example.com/payment/return"` (hard-coded placeholder), and `RequestOptions.IdempotencyKey = payment.IdempotencyKey` (the same key is used in the DB unique index and forwarded to Stripe).
* PaymentIntent status mapping:

| Stripe status | Result |
|---|---|
| `succeeded` | Success, `ProviderPaymentId = intent.Id` |
| `requires_action` | Failure: `"3D Secure authentication required. Use the client SDK to complete confirmation."` |
| anything else | Failure: `"Payment not completed. Stripe status: {status}"` |
| `StripeException` | Failure with `StripeError.Message` (or exception message) |

* **3D Secure**: not completed server-side. A `requires_action` intent is treated as a failure and the payment becomes `Failed` (the PaymentIntent id is not stored). There is no webhook endpoint or follow-up confirmation flow.
* **Refund**: `RefundService.CreateAsync` with `PaymentIntent = ProviderPaymentId`, `Amount = round(amount * 100)`; success only if the refund status is `"succeeded"`; failures return `StripeError.Message`. No idempotency key is sent for refunds.
* The client sends a Stripe **PaymentMethod id** (`pm_...`, created with Stripe.js or a test token such as `pm_card_visa`); card data never reaches this service.

### Idempotency

* Creating the same `idempotencyKey` twice returns `409 Conflict` (`payment.duplicate`): checked in the handler and guaranteed by the unique index.
* The key is also the Stripe PaymentIntent idempotency key.
* Re-processing an already processed payment fails with `payment.invalid_state` (400) because `MarkProcessing` requires `Pending`.
* Note: a concurrent double-create with the same key could pass the handler check and fail on the unique index (surfacing as a 500); no explicit handling exists for that race.

### Background job: `ExpiredPaymentJob`

A `BackgroundService` (registered with `AddHostedService`) that loops forever with a fixed 1-minute delay (hard-coded, not configurable):

1. `IPaymentRepository.GetExpiredPendingPaymentsAsync` -> `Status == Pending && ExpiresAt <= UtcNow`.
2. For each: `MarkCancelled()`, publish `payment.cancelled` (`Reason` = `"Payment expired (auto-cancelled)"`), remove `payment:{id}` and `payment:order:{orderId}`.
3. One `SaveChangesAsync` for the whole batch, then remove prefix `payments:`.

Errors are logged and the loop continues. Events are published before the batch is saved, so a save failure would cause the same payments to be cancelled and re-published on the next tick.

### MediatR logging behavior

`LoggingBehavior<TRequest,TResponse>` logs `Handling {RequestName} | Payload: {json}` for every request, then elapsed time or the exception. **The full command payload is logged, including `PaymentToken` and `RecipientEmail`.**

---

## 8. Configuration

Configuration comes from `appsettings.json`, `appsettings.{Environment}.json` and environment variables (ASP.NET Core double-underscore syntax, e.g. `Database__ConnectionString`).

| Key / env var | Default (`appsettings.json`) | `appsettings.Development.json` | Meaning |
|---|---|---|---|
| `ServiceName` | `payments-service` | same | OpenTelemetry service name |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://jaeger:4317` | `http://localhost:4317` | OTLP gRPC endpoint for traces and logs (also hard-coded fallback `http://jaeger:4317`) |
| `Database:ConnectionString` | `Host=postgres;Port=5432;Database=payments;Username=postgres;Password=postgres` | `Host=localhost;Port=5433;Database=payments;Username=postgres;Password=postgres` | Npgsql connection string (required) |
| `RabbitMQ:Host` | `rabbitmq` | `localhost` | Broker host |
| `RabbitMQ:Port` | `5672` | `5672` | AMQP port |
| `RabbitMQ:Username` | `guest` | `guest` | Broker user |
| `RabbitMQ:Password` | `guest` | `guest` | Broker password |
| `RabbitMQ:VirtualHost` | `/` | `/` | Virtual host |
| `Redis:ConnectionString` | `redis:6379` | `localhost:6379` | StackExchange.Redis configuration string |
| `Redis:DefaultTtlSeconds` | `300` (code default; not in the JSON files) | - | Fallback cache TTL |
| `Stripe:SecretKey` | empty | empty | Stripe secret API key (`sk_test_...`); **must be provided**, never commit |
| `Stripe:PublishableKey` | a Stripe test publishable key | same | Bound to `StripeOptions` but **not used** by any code |
| `Stripe:WebhookSecret` | empty | empty | Bound to `StripeOptions` but **not used** (no webhook endpoint) |
| `ASPNETCORE_ENVIRONMENT` | - | `Development` (launch profiles, compose) | `Development` triggers automatic `Database.Migrate()` on startup; `Testing` disables health checks and migrate-on-start |
| `ASPNETCORE_URLS` | - | - | Set to `http://+:8080` in docker compose |

Fixed in code (not configurable): payment expiry 30 min, expiry job interval 1 min, cache TTLs 5 min (by id / by order) and 2 min (lists), Stripe `ReturnUrl`, exchange name `payments-exchange`.

### Docker compose and `docker/template.env`

The `payments` service in `docker/docker-compose.yml` sets:

| Compose environment | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `ASPNETCORE_URLS` | `http://+:8080` |
| `Database__ConnectionString` | `Host=postgres;Port=5432;Database=payments;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}` |
| `RabbitMQ__Host` / `__Port` / `__VirtualHost` | `rabbitmq` / `5672` / `/` |
| `RabbitMQ__Username` / `__Password` | `${RABBITMQ_USER}` / `${RABBITMQ_PASSWORD}` |
| `Redis__ConnectionString` | `redis:6379` |
| `Stripe__SecretKey` | `${STRIPE_SECRET_KEY}` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://jaeger:4317` |

Variables from `docker/template.env` relevant to this service (copy it to `docker/.env`; `.env` is git-ignored):

| Variable | Template value | Used for |
|---|---|---|
| `PAYMENTS_PORT` | `5135` | Host port mapped to container port 8080 |
| `STRIPE_SECRET_KEY` | empty | Injected as `Stripe__SecretKey` |
| `POSTGRES_USER` / `POSTGRES_PASSWORD` | `postgres` / `postgres` | DB credentials |
| `POSTGRES_PORT` | `5433` | Host port of PostgreSQL (container 5432) |
| `RABBITMQ_USER` / `RABBITMQ_PASSWORD` | empty (fill in) | Broker credentials, also used to create the broker user |
| `RABBITMQ_PORT` / `RABBITMQ_UI_PORT` | `5672` / `15672` | Broker ports |
| `REDIS_PORT` | `6379` | Redis host port |
| `OTEL_UI_PORT` / `OTEL_GRPC_PORT` / `OTEL_HTTP_PORT` | `16686` / `4317` / `4318` | Jaeger |

### Ports

| Context | Port |
|---|---|
| Container (`Dockerfile` `EXPOSE 8080`, `ASPNETCORE_URLS`) | `8080` |
| Docker compose host port | `5135` (`PAYMENTS_PORT`) |
| Native `dotnet run` (`launchSettings.json`) | `http://localhost:5072`; `https://localhost:7086` (https profile) |
| Via API Gateway | `http://localhost:5133/payments/...` (`GATEWAY_PORT`) |

---

## 9. Observability

| Signal | Details |
|---|---|
| Traces | OpenTelemetry tracing with ASP.NET Core (exceptions recorded), `HttpClient` (exceptions recorded), EF Core and StackExchange.Redis instrumentation, plus the custom `rabbitmq.publish` activity. The tracer registers `AddService(ServiceName)` and exports via OTLP to `OTEL_EXPORTER_OTLP_ENDPOINT`. Note: `Payments.Messaging` is created as an `ActivitySource` but is **not** added with `AddSource(...)` in `OpenTelemetrySetup`, so those spans are likely not exported. |
| Logs | Console provider plus an OpenTelemetry log exporter (OTLP, same endpoint; scopes, formatted message and state values included). Jaeger stores traces only, so exported logs are only useful with a collector that accepts OTLP logs. |
| Request logging | `RequestLoggingMiddleware`: `HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms`. |
| Correlation id | `CorrelationIdMiddleware` reads or generates `X-Correlation-Id`, stores it in `HttpContext.Items["X-Correlation-Id"]` and sets it on the response. It is not added to log scopes, outgoing Stripe calls or RabbitMQ messages. |
| Health | `GET /health` (PostgreSQL, Redis, RabbitMQ). Neither the Dockerfile nor docker compose defines a container `HEALTHCHECK`. |
| Tools | Jaeger UI `http://localhost:16686`, RabbitMQ management UI `http://localhost:15672`. |

---

## 10. Running locally

Prerequisites: .NET 10 SDK, Docker Desktop, a Stripe account in **test mode**.

### Option A: everything in Docker

```bash
cd docker
cp template.env .env       # then fill in RABBITMQ_USER, RABBITMQ_PASSWORD, STRIPE_SECRET_KEY, ...
docker compose up -d --build payments
```

Compose starts the dependencies (`postgres`, `rabbitmq`, `redis`, `jaeger`); because `ASPNETCORE_ENVIRONMENT=Development`, migrations are applied automatically at startup. The service is then reachable at `http://localhost:5135` (and through the gateway at `http://localhost:5133/payments`).

### Option B: infrastructure in Docker, service native

```bash
cd docker
docker compose up -d postgres rabbitmq redis jaeger

cd ../Payments
dotnet user-secrets --project Payments set "Stripe:SecretKey" "sk_test_..."
dotnet run --project Payments            # http://localhost:5072, Development environment
```

`appsettings.Development.json` targets `localhost` with PostgreSQL on **5433** (the compose host port), RabbitMQ on 5672, Redis on 6379 and OTLP on 4317. The RabbitMQ credentials in that file are `guest`/`guest`; if the compose broker was created with different `RABBITMQ_USER`/`RABBITMQ_PASSWORD`, override them (`RabbitMQ__Username`, `RabbitMQ__Password`) or user-secrets. In `Development` the app runs `Database.Migrate()` on startup, which also creates the `payments` database.

Try it with `Payments/Payments/Payments.http`:

```bash
# create
curl -X POST http://localhost:5072/payments -H "Content-Type: application/json" -d '{
  "orderId":"d290f1ee-6c54-4b01-90e6-d701748f0851",
  "userId":"a290f1ee-6c54-4b01-90e6-d701748f0852",
  "amount":49.99,"currency":"EUR","method":"CreditCard",
  "idempotencyKey":"order-d290f1ee-payment-001","paymentToken":"pm_card_visa"}'
# process / refund / read
curl -X POST http://localhost:5072/payments/<paymentId>/process
curl -X POST http://localhost:5072/payments/<paymentId>/refund -H "Content-Type: application/json" -d '{"amount":10.00}'
curl http://localhost:5072/payments/<paymentId>
```

### Stripe test setup

1. In the Stripe Dashboard (test mode) copy the **secret key** (`sk_test_...`) into `STRIPE_SECRET_KEY` (compose) or `Stripe:SecretKey` (user-secrets / env var `Stripe__SecretKey`).
2. Obtain a PaymentMethod id (`pm_...`) for `paymentToken`: either server-side test tokens such as `pm_card_visa` / `pm_card_chargeDeclined`, or Stripe.js `createPaymentMethod` on the client.
3. Useful test cards (used through Stripe.js): `4242 4242 4242 4242` succeeds, `4000 0000 0000 0002` is declined, `4000 0025 0000 3155` requires 3D Secure (this service will mark it `Failed`, see above).
4. No webhook setup is needed or supported.

### EF Core migrations

Run from the `Payments/` solution folder:

```bash
# add a migration
dotnet ef migrations add <Name> --project Infrastructure --startup-project Payments --output-dir Persistence/Migrations

# apply to the database configured for the startup project
dotnet ef database update --project Infrastructure --startup-project Payments

# remove the last (unapplied) migration
dotnet ef migrations remove --project Infrastructure --startup-project Payments
```

Outside the `Development` environment migrations are **not** applied automatically; run `dotnet ef database update` (or apply them in your deployment pipeline).

---

## 11. Tests

```bash
cd Payments
dotnet test UnitTests
dotnet test IntegrationTests      # requires a running Docker daemon
```

| Project | What it covers | Needs |
|---|---|---|
| `UnitTests` | `Payment` / `PaymentTransaction` domain rules and state transitions; handler tests for create, process, refund and the three queries (Moq for repository, publisher, cache and provider); `PaymentRepository` against in-memory SQLite; `RecipientEmailResolver` | Nothing external |
| `IntegrationTests` | `PaymentsApiFactory` (`WebApplicationFactory<Program>`, environment `Testing`) starts Testcontainers PostgreSQL 17 (`payments_test`), Redis 7 and RabbitMQ 3-management, applies migrations and exercises `POST /payments` (incl. duplicate idempotency key -> 409), `GET /payments/{id}`, `GET /payments/by-order/{orderId}`, `GET /payments` (list and status filter) | Docker. Stripe is configured with a fake key; process and refund are **not** covered end to end |

In the `Testing` environment `/health` is not mapped and migrate-on-start is skipped (the factory migrates explicitly).

---

## 12. Security notes, known gaps and inconsistencies

Security:

* **No authentication/authorization in the service.** Any caller that can reach port 8080 (or the host port `5135`) can create, process, refund and list payments. Access control, if any, must be enforced by the API Gateway; `userId` in the request body is trusted as-is.
* `appsettings*.json` contain default credentials (`postgres/postgres`, `guest/guest`) and a Stripe **publishable** test key; the Stripe secret key is intentionally empty and must come from secrets or environment variables.
* `PaymentToken` (Stripe PaymentMethod id) is stored in plain text in `Payments.PaymentToken` and is written to logs by `LoggingBehavior`.
* `ExceptionMiddleware` returns raw exception messages for unexpected errors (HTTP 500).
* No input validation (amount > 0, currency format, empty idempotency key/token) before persistence; `amount * 100` assumes a 2-decimal currency (zero-decimal currencies would be mis-scaled).

Known gaps / things to be aware of:

* No Stripe webhook handling; `Stripe:WebhookSecret` and `Stripe:PublishableKey` are unused. 3D Secure outcomes are not completed.
* Stripe `ReturnUrl` is a hard-coded placeholder (`https://example.com/payment/return`).
* No transactional outbox: events are published after the DB commit; messages are not persistent and no publisher confirms are configured.
* No optimistic-concurrency token; concurrent `process` calls rely on Stripe idempotency and on the unique key.
* `payment.created` has no known consumer and carries no `RecipientEmail`.
* Cache invalidation is slightly inconsistent: a failed charge does not clear `payment:order:{orderId}`, and the "expired on process" path invalidates no cache keys, so by-order reads may be stale for up to 5 minutes.
* `GET /payments/by-order/{orderId}` is non-deterministic when an order has more than one payment (no unique index on `OrderId`).
* `/process` returns `200 OK` even when the charge is declined.
* Migration `InitialCreateFirst1` lives in `Infrastructure/Payments/Migrations` (namespace `Infrastructure.Payments.Migrations`) instead of `Persistence/Migrations`, and its name does not describe its content (it only adds `RecipientEmail`).
* `Microsoft.AspNetCore.OpenApi` and `AspNetCore.HealthChecks.UI.Client` are referenced but unused; `AddEndpointsApiExplorer()` is registered but no OpenAPI document is mapped.
* `Payments.Messaging` `ActivitySource` is not registered with the tracer provider.
