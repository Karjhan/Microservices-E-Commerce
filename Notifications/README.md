# Notifications Service

Event-driven **email notification** microservice for the 3D-printing e-commerce platform.
It consumes integration events from RabbitMQ (published by Auth, Orders, Payments and
Inventory), renders Jinja2 email templates, delivers them over SMTP, and stores an
auditable delivery history in PostgreSQL. A small read-only HTTP API exposes a health
check and that delivery history.

The service ships as **one Docker image that runs in two processes**:

| Process | Compose service | Role |
|---|---|---|
| API | `notifications-api` | FastAPI app: `GET /health`, `GET /notifications`, `GET /notifications/{id}` |
| Worker | `notifications-worker` | RabbitMQ consumer that renders and sends emails |

> Only email is implemented. `ChannelType` also lists `Sms` and `Push`, but nothing sends
> through them and every stored row has `channel = "Email"`.

---

## Table of contents

1. [Role in the system](#1-role-in-the-system)
2. [Tech stack](#2-tech-stack)
3. [Project layout and architecture](#3-project-layout-and-architecture)
4. [HTTP API](#4-http-api)
5. [Messaging (RabbitMQ)](#5-messaging-rabbitmq)
6. [Data storage (PostgreSQL)](#6-data-storage-postgresql)
7. [Templates, SMTP and retry policy](#7-templates-smtp-and-retry-policy)
8. [Configuration](#8-configuration)
9. [Observability](#9-observability)
10. [Running locally](#10-running-locally)
11. [Tests](#11-tests)
12. [Known gaps and caveats](#12-known-gaps-and-caveats)

---

## 1. Role in the system

Notifications is a pure **downstream consumer**. It owns no business data, publishes no
business events, and nothing in the platform calls it synchronously except the
(optional) history API.

```text
 Auth ──user.registered──────────► ecommerce.events ─┐
 Orders ─order.*─────────────────► orders-exchange ──┤
 Payments ─payment.*─────────────► payments-exchange ┼─► queue notifications.events ─► Worker ─► SMTP (MailHog in dev)
 Inventory ─inventory.depleted───► inventory-exchange┘                                   │
                                                                                         ├─► PostgreSQL `notifications` DB
 Client ─► API Gateway /notifications/... ─► notifications-api (history, read-only) ─────┘
```

| Related component | Relationship |
|---|---|
| **Auth** | Publishes `user.registered` on `ecommerce.events` (welcome email). |
| **Orders** | Publishes `order.*` on `orders-exchange` (customer emails). |
| **Payments** | Publishes `payment.*` on `payments-exchange` (customer emails). |
| **Inventory** | Publishes `inventory.depleted` on `inventory-exchange` (operator email). |
| **API Gateway** | YARP route `/notifications/{**catch-all}` forwards to `http://notifications-api:8200` (see [4.3](#43-exposure-through-the-api-gateway)). |
| **PostgreSQL** | Own database `notifications` on the shared Postgres container. |
| **MailHog** | Dev SMTP sink that captures outgoing mail. |
| **Jaeger** | OTLP trace collector. |

Customer emails are sent to the address carried **in the event** (`recipientEmail`, or
`email` for `user.registered`). The service does not look users up in Auth/Orders.
Operator (low-stock) emails go to the addresses in `NOTIFICATIONS_OPERATOR_EMAILS`.

---

## 2. Tech stack

| Area | Technology |
|---|---|
| Language | Python `>=3.11` (Docker image: `python:3.11-slim`) |
| Web framework | FastAPI `>=0.115`, Uvicorn (`uvicorn[standard]`) |
| Validation / settings | Pydantic `>=2.9`, `pydantic-settings` |
| Database | PostgreSQL via SQLAlchemy 2.x async (`sqlalchemy[asyncio]`) + `asyncpg`; Alembic migrations |
| Messaging | RabbitMQ via `aio-pika` (`connect_robust`) |
| Templates | `Jinja2` |
| Email | `aiosmtplib` (stdlib `EmailMessage`, multipart text + HTML) |
| Retry | `tenacity` |
| Logging | `structlog` |
| Tracing | OpenTelemetry API/SDK, OTLP gRPC exporter, instrumentation for FastAPI, httpx, aio-pika, logging |
| Dev/test | `pytest`, `pytest-asyncio`, `pytest-cov`, `httpx`, `ruff` (line length 100, `py311`), `aiosqlite` |

Dependencies are declared in `pyproject.toml` (project `notifications-service` `0.1.0`)
and mirrored in `requirements.txt` (used by the Dockerfile; it additionally lists an
unpinned `httpx` and has no dev tools).

---

## 3. Project layout and architecture

```text
Notifications/
├── pyproject.toml / requirements.txt
├── alembic.ini
├── alembic/
│   ├── env.py                                   # async env, URL taken from Settings.database_url
│   └── versions/20260605120000_initial_notifications.py
├── docker/
│   ├── Dockerfile                               # python:3.11-slim, EXPOSE 8200
│   └── entrypoint.sh                            # SERVICE_MODE = api | worker | both
├── app/
│   ├── api/
│   │   ├── main.py                              # FastAPI app, CORS (*), lifespan
│   │   ├── dependencies.py                      # get_db_session
│   │   ├── schemas.py                           # HealthResponse, NotificationResponse, NotificationListResponse
│   │   └── routes/{health.py, notifications.py}
│   ├── workers/
│   │   ├── consumer.py                          # EventConsumer: topology, routing, ack/nack
│   │   └── __main__.py
│   ├── contracts/
│   │   ├── events.py                            # Pydantic models of consumed events
│   │   └── normalize.py                         # PascalCase -> camelCase key normalisation
│   ├── handlers/                                # one handler per routing key (11)
│   ├── application/
│   │   ├── notification_dispatcher.py           # render, persist, send+retry, DLQ publish
│   │   ├── idempotency_service.py               # "<routingKey>:<entityId>" keys
│   │   └── template_renderer.py                 # Jinja2 environment
│   ├── delivery/{base.py, email_channel.py}     # DeliveryChannel protocol, SMTP sender, error types
│   ├── domain/enums.py
│   ├── persistence/{database.py, models.py, repositories.py}
│   ├── core/{settings.py, logging.py, telemetry.py}
│   └── templates/<slug>/{subject.j2, body.html.j2, body.txt.j2}   # 11 templates
└── tests/{unit, integration, conftest.py}
```

**Processing pipeline (worker):**

```text
message -> EventConsumer._on_message
        -> json.loads -> normalize_wire_payload
        -> handler for routing key (validates payload with a Pydantic model)
        -> NotificationDispatcher.dispatch / dispatch_to_recipients / skip_invalid_payload
              idempotency check -> render template -> insert notifications row
              -> SMTP send (tenacity retry) -> update status -> insert processed_event_logs -> commit
        -> message.ack()
```

---

## 4. HTTP API

The API is **read-only** and has **no authentication** at the service level. CORS allows
all origins, methods and headers. FastAPI's default `/docs`, `/redoc` and `/openapi.json`
are available when talking to the service directly (not through the gateway).

### 4.1 `GET /health`

Checks PostgreSQL (`SELECT 1`), RabbitMQ (connect, 5 s timeout) and SMTP (connect + quit,
5 s timeout). It **always returns HTTP 200**; failures are reported in the body.
`status` is `"healthy"` only when all three are healthy, otherwise `"degraded"`.

```json
{
  "status": "healthy",
  "postgres": "healthy",
  "rabbitmq": "healthy",
  "smtp": "healthy"
}
```

A failing dependency is reported as `"unhealthy: <exception text>"`, for example
`"smtp": "unhealthy: Connection refused"`.

### 4.2 Notification history

#### `GET /notifications`

Paged list, newest first (`created_at DESC`).

| Query param | Type | Default | Constraints | Meaning |
|---|---|---|---|---|
| `eventId` | UUID | - | valid UUID | Entity id of the source event. Matches rows whose `idempotencyKey` ends with `:<eventId>` (orderId, paymentId, userId or productId, depending on event). |
| `eventType` | string | - | - | Exact match on routing key, e.g. `order.created`. |
| `recipient` | string | - | - | Exact match on recipient address. |
| `status` | enum | - | `Pending`, `Sent`, `Failed`, `DeadLettered` | Delivery status. |
| `page` | int | `1` | `>= 1` | 1-based page. |
| `pageSize` | int | `20` | `1..100` | Page size. |

Status codes: `200` on success; `422` for invalid parameters (bad UUID, unknown `status`,
out-of-range `page`/`pageSize`).

```json
{
  "items": [
    {
      "id": "6f1d3a52-3c0e-4c5e-9d52-0d9c1b1f7a11",
      "idempotencyKey": "order.created:2b5a1e0c-7f2e-4d79-8f2e-5e8d3b6f9c10",
      "eventType": "order.created",
      "notificationType": "OrderConfirmation",
      "channel": "Email",
      "recipient": "customer@example.com",
      "subject": "Order 2b5a1e0c-7f2e-4d79-8f2e-5e8d3b6f9c10 confirmed",
      "bodyPreview": "Order 2b5a1e0c-7f2e-4d79-8f2e-5e8d3b6f9c10 confirmed. Total: 42.5 EUR",
      "status": "Sent",
      "attemptCount": 1,
      "correlationId": "c0a8f1d2-1111-2222-3333-444455556666",
      "errorDetails": null,
      "createdAt": "2026-11-24T12:00:00.123456Z",
      "sentAt": "2026-11-24T12:00:00.456789Z",
      "updatedAt": "2026-11-24T12:00:00.456789Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1
}
```

`totalPages` is computed as `ceil(totalCount / pageSize)`.

#### `GET /notifications/{notification_id}`

Returns a single `NotificationResponse` (same shape as an item above).

| Status | Condition |
|---|---|
| `200` | Found. |
| `404` | `{"detail": "Notification not found"}` |
| `422` | `notification_id` is not a UUID. |

`NotificationResponse` fields: `id`, `idempotencyKey`, `eventType`, `notificationType`
(see [6.3](#63-enumerations)), `channel`, `recipient`, `subject`, `bodyPreview`, `status`,
`attemptCount`, `correlationId`, `errorDetails`, `createdAt`, `sentAt`, `updatedAt`.

### 4.3 Exposure through the API Gateway

`ApiGateway/ApiGateway/appsettings.json` (and `appsettings.Development.json`) define:

| Item | Value |
|---|---|
| Route `notifications` | `Path: /notifications/{**catch-all}` -> cluster `notifications-cluster`, **no path transform** |
| Cluster destination | `http://notifications-api:8200` |
| `Services:Notifications` | `http://notifications-api:8200` |

So through the gateway (host port `GATEWAY_PORT`, default `5133`):

| Gateway URL | Reaches |
|---|---|
| `GET http://localhost:5133/notifications` | `GET /notifications` |
| `GET http://localhost:5133/notifications/{id}` | `GET /notifications/{id}` |

Notes:

- `/health` is **not** routed through the gateway (the gateway has its own health
  endpoints); call `notifications-api` directly on its host port.
- The route has no authorization policy configured in `appsettings.json`, so it is not
  protected by JWT at the gateway, and the service itself does no auth. The history
  contains recipient e-mail addresses, so treat this as an open item for production.

---

## 5. Messaging (RabbitMQ)

### 5.1 Topology

All declared by `EventConsumer.start()` in `app/workers/consumer.py` on startup
(`connect_robust`, channel `prefetch_count=10`).

| Object | Name (default) | Type / properties |
|---|---|---|
| Work queue | `notifications.events` (`RABBITMQ_QUEUE`) | durable; `x-dead-letter-exchange=notifications-dlx`, `x-dead-letter-routing-key=notifications.dlq` |
| Dead-letter exchange | `notifications-dlx` (`RABBITMQ_DLX`) | direct, durable |
| Dead-letter queue | `notifications.dlq` (`RABBITMQ_DLQ`) | durable; bound to the DLX with routing key `notifications.dlq` |

Source exchanges are declared as **topic, durable** (they must match the publishers'
declarations) and the work queue is bound as follows:

| Exchange | Routing keys bound |
|---|---|
| `ecommerce.events` | `user.registered` |
| `orders-exchange` | `order.created`, `order.cancelled`, `order.shipped`, `order.delivered`, `order.payment.failed` |
| `payments-exchange` | `payment.completed`, `payment.failed`, `payment.cancelled`, `payment.refunded` |
| `inventory-exchange` | `inventory.depleted` |

**Published messages:** the service publishes no business events. The only thing it
publishes is a JSON message to `notifications-dlx` (routing key `notifications.dlq`) when
SMTP retries are exhausted (see [5.4](#54-consumer-behaviour)).

### 5.2 Wire format and normalisation

- Body must be UTF-8 JSON.
- `normalize_wire_payload` lower-cases the first letter of every key recursively, so
  PascalCase (`OrderId`) and camelCase (`orderId`) payloads are both accepted.
- Pydantic models (`app/contracts/events.py`) use camelCase aliases and also accept the
  snake_case field names (`populate_by_name=True`). Unknown fields are ignored.
- IDs are validated as UUIDs, timestamps as ISO-8601 datetimes.
- Correlation id: read from message headers `X-Correlation-Id`, `x-correlation-id` or
  `correlationId` (first found). It is stored in `notifications.correlation_id`, bound to
  log context, and forwarded as `X-Correlation-Id` on DLQ messages. If absent, a random id
  is used in the logs only and `correlation_id` stays `NULL` in the database.

### 5.3 Consumed events

"Required" fields make Pydantic validation fail if missing. "Optional" fields may be
absent. Every customer-facing event additionally needs a non-empty `recipientEmail`;
if it is missing the notification is recorded as skipped (see [5.4](#54-consumer-behaviour)).
Fields not listed in "Used" are validated/accepted but not rendered.

| Routing key | Exchange | Required fields | Optional fields | Template (`NotificationType`) | Recipient | Idempotency key |
|---|---|---|---|---|---|---|
| `user.registered` | `ecommerce.events` | `userId` (UUID), `email` (string) | `role` | `welcome` (`Welcome`) | customer: `email` | `user.registered:<userId>` |
| `order.created` | `orders-exchange` | `orderId`, `userId`, `totalAmount` (number), `currency`, `itemCount` (int) | `createdAt`, `items[]` (`productId` UUID, `quantity` int), `recipientEmail`, `lineSummary` | `order_confirmation` (`OrderConfirmation`) | customer: `recipientEmail` | `order.created:<orderId>` |
| `order.cancelled` | `orders-exchange` | `orderId`, `userId` | `reason`, `cancelledAt`, `recipientEmail` | `order_cancellation` (`OrderCancellation`) | customer | `order.cancelled:<orderId>` |
| `order.shipped` | `orders-exchange` | `orderId`, `userId` | `shippedAt`, `recipientEmail` | `shipping_update` (`ShippingUpdate`) | customer | `order.shipped:<orderId>` |
| `order.delivered` | `orders-exchange` | `orderId`, `userId` | `deliveredAt`, `recipientEmail` | `delivery_confirmation` (`DeliveryConfirmation`) | customer | `order.delivered:<orderId>` |
| `order.payment.failed` | `orders-exchange` | `orderId`, `userId` | `reason`, `failedAt`, `recipientEmail` | `checkout_payment_failure` (`CheckoutPaymentFailure`) | customer | `order.payment.failed:<orderId>` |
| `payment.completed` | `payments-exchange` | `paymentId`, `orderId`, `userId`, `amount` (number), `currency` | `providerPaymentId`, `completedAt`, `recipientEmail` | `payment_receipt` (`PaymentReceipt`) | customer | `payment.completed:<paymentId>` |
| `payment.failed` | `payments-exchange` | `paymentId`, `orderId`, `userId`, `amount`, `currency` | `reason`, `failedAt`, `recipientEmail` | `payment_failure` (`PaymentFailure`) | customer | `payment.failed:<paymentId>` |
| `payment.cancelled` | `payments-exchange` | `paymentId`, `orderId`, `userId` | `reason`, `cancelledAt`, `recipientEmail` | `payment_cancellation` (`PaymentCancellation`) | customer | `payment.cancelled:<paymentId>` |
| `payment.refunded` | `payments-exchange` | `paymentId`, `orderId`, `userId`, `refundAmount`, `totalRefunded`, `originalAmount`, `currency`, `isFullRefund` (bool) | `refundedAt`, `recipientEmail` | `refund_confirmation` (`RefundConfirmation`) | customer | `payment.refunded:<paymentId>` |
| `inventory.depleted` | `inventory-exchange` | `productId` | `depletedAt` | `low_stock_alert` (`LowStockAlert`) | **operators**: `NOTIFICATIONS_OPERATOR_EMAILS` | `inventory.depleted:<productId>` |

Template variables actually passed per event (by the handlers):

| Template | Context keys |
|---|---|
| `welcome` | `userId`, `email` |
| `order_confirmation` | `orderId`, `lineSummary`, `totalAmount`, `currency` |
| `order_cancellation` | `orderId`, `reason` |
| `shipping_update` | `orderId`, `shippedAt` |
| `delivery_confirmation` | `orderId`, `deliveredAt` |
| `checkout_payment_failure` | `orderId`, `reason` |
| `payment_receipt` | `paymentId`, `orderId`, `amount`, `currency`, `completedAt` |
| `payment_failure` | `paymentId`, `orderId`, `amount`, `currency`, `reason` |
| `payment_cancellation` | `paymentId`, `orderId`, `reason` |
| `refund_confirmation` | `paymentId`, `orderId`, `refundAmount`, `currency`, `isFullRefund` (not used by the template text) |
| `low_stock_alert` | `productId`, `depletedAt` |

`lineSummary` for `order.created` is the event's `lineSummary` if present; otherwise it is
built from `items` as `"<quantity>× <productId>, ..."`; otherwise empty. Timestamps are
rendered with `datetime.isoformat()`.

**Example payloads**

`user.registered` (`ecommerce.events`):

```json
{ "userId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b", "email": "customer@example.com", "role": "Customer" }
```

`order.created` (`orders-exchange`):

```json
{
  "orderId": "2b5a1e0c-7f2e-4d79-8f2e-5e8d3b6f9c10",
  "userId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b",
  "totalAmount": 42.5,
  "currency": "EUR",
  "itemCount": 2,
  "createdAt": "2026-11-24T12:00:00Z",
  "items": [ { "productId": "0a1b2c3d-4e5f-6789-abcd-ef0123456789", "quantity": 2 } ],
  "recipientEmail": "customer@example.com",
  "lineSummary": "2x PLA Filament 1kg"
}
```

`payment.completed` (`payments-exchange`):

```json
{
  "paymentId": "5c4d3e2f-1a0b-4c9d-8e7f-6a5b4c3d2e1f",
  "orderId": "2b5a1e0c-7f2e-4d79-8f2e-5e8d3b6f9c10",
  "userId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b",
  "amount": 42.5,
  "currency": "EUR",
  "providerPaymentId": "pi_example",
  "completedAt": "2026-11-24T12:01:00Z",
  "recipientEmail": "customer@example.com"
}
```

`payment.refunded` (`payments-exchange`):

```json
{
  "paymentId": "5c4d3e2f-1a0b-4c9d-8e7f-6a5b4c3d2e1f",
  "orderId": "2b5a1e0c-7f2e-4d79-8f2e-5e8d3b6f9c10",
  "userId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b",
  "refundAmount": 10.0,
  "totalRefunded": 10.0,
  "originalAmount": 42.5,
  "currency": "EUR",
  "isFullRefund": false,
  "refundedAt": "2026-11-25T09:00:00Z",
  "recipientEmail": "customer@example.com"
}
```

`inventory.depleted` (`inventory-exchange`):

```json
{ "productId": "0a1b2c3d-4e5f-6789-abcd-ef0123456789", "depletedAt": "2026-11-24T13:00:00Z" }
```

### 5.4 Consumer behaviour

| Situation | Behaviour |
|---|---|
| Routing key has no handler | Logged as warning, message **acked**. |
| Body is not valid JSON | `nack(requeue=False)` -> broker dead-letters it to `notifications-dlx` -> `notifications.dlq`. |
| Required customer email missing (`recipientEmail`, or `email` for `user.registered`) | `skip_invalid_payload`: stores a `notifications` row (`subject="(skipped)"`, `status=Failed`, `attempt_count=1`, `error_details="Missing recipientEmail on <routing key>"`) and a `processed_event_logs` row with outcome `SkippedInvalidPayload`; message **acked**. |
| `inventory.depleted` and `NOTIFICATIONS_OPERATOR_EMAILS` has no addresses | Same skip path with reason `NOTIFICATIONS_OPERATOR_EMAILS not configured`. |
| Duplicate event (idempotency key already in `processed_event_logs`) | Logged, no email sent, message **acked**. |
| Email sent | `status=Sent`, `sent_at` set, `processed_event_logs.outcome=Sent`, message **acked**. |
| Permanent SMTP failure (recipient refused, auth failure) | No retry. `status=Failed`, `error_details` set, `attempt_count = MAX_DELIVERY_ATTEMPTS`, outcome `FailedPermanently`, message **acked**. |
| Transient SMTP failure after all retries (default 4 attempts) | Message is published to `notifications-dlx` (routing key `notifications.dlq`), `status=DeadLettered`, outcome `FailedPermanently`, original message **acked**. |
| Any unexpected exception (including Pydantic validation errors for missing required fields, DB errors) | Logged, `nack(requeue=True)` -> the message is redelivered immediately. There is no retry limit for this path. |

**Idempotency.** Key = `"<routingKey>:<entityId>"` (entity id column in the table above).
Before doing anything the dispatcher checks `processed_event_logs` for the key. The
`processed_event_logs` row is written in the same transaction as the final notification
state. `IdempotencyService.mark_processed` also tolerates a unique-key race
(`IntegrityError` -> rollback, returns `False`). Because keys use the entity id only,
each entity gets **at most one** email per routing key for its lifetime (for example one
`inventory.depleted` alert per product, ever).

**DLQ message body** (`notifications.dlq`, `application/json`, header `X-Correlation-Id`
when known):

```json
{
  "routingKey": "user.registered",
  "entityId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b",
  "payload": { "userId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b", "email": "customer@example.com" },
  "error": "Connection refused"
}
```

`payload` is the **template context** (not the original event). Nothing in this repo
consumes or replays `notifications.dlq`.

**Multi-recipient dispatch** (`dispatch_to_recipients`, used only by `inventory.depleted`):
recipients are emailed sequentially; the single `notifications` row stores them
comma-separated in `recipient`.

---

## 6. Data storage (PostgreSQL)

Database `notifications` on the shared Postgres container (created by
`docker/postgres-init/notifications.sql`: `CREATE DATABASE notifications;`, which only runs
on first initialisation of the Postgres data volume). Access is through SQLAlchemy async
(`postgresql+asyncpg`). No other data store (no Redis, no object storage) is used.

Delivery history is stored **one row per notification** in `notifications`: subject,
a 500-character body preview (plain-text body, falling back to HTML), status, attempts and
error text. The full rendered body is **not** stored.

### 6.1 Table `notifications`

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK, generated in app (`uuid4`) |
| `idempotency_key` | `varchar(256)` | no | **unique**; `<routingKey>:<entityId>` |
| `event_type` | `varchar(64)` | no | routing key, e.g. `order.created` |
| `notification_type` | `varchar(64)` | no | `NotificationType` value |
| `channel` | `varchar(16)` | no | always `Email` in practice |
| `recipient` | `varchar(320)` | no | one address, or comma-joined list for operator alerts; empty string on skipped rows with no address |
| `subject` | `varchar(500)` | no | rendered subject; `(skipped)` for skipped rows |
| `body_preview` | `varchar(500)` | yes | first 500 chars of text (else HTML) body |
| `status` | `varchar(32)` | no | `Pending` / `Sent` / `Failed` / `DeadLettered` |
| `attempt_count` | `integer` | no | see caveats in [12](#12-known-gaps-and-caveats) |
| `correlation_id` | `varchar(64)` | yes | from message headers |
| `error_details` | `text` | yes | last error / skip reason |
| `created_at` | `timestamptz` | no | server default `now()` |
| `sent_at` | `timestamptz` | yes | set when `Sent` |
| `updated_at` | `timestamptz` | no | server default `now()`, `onupdate=now()` in the ORM |

Indexes: `ix_notifications_event_type_created_at (event_type, created_at)`,
`ix_notifications_recipient_created_at (recipient, created_at)`,
`ix_notifications_status (status)`, plus the unique constraint on `idempotency_key`.

### 6.2 Table `processed_event_logs`

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | no | PK |
| `idempotency_key` | `varchar(256)` | no | unique (constraint + unique index `ix_processed_event_logs_idempotency_key`) |
| `event_type` | `varchar(64)` | no | routing key |
| `processed_at` | `timestamptz` | no | server default `now()` |
| `outcome` | `varchar(32)` | no | `Sent`, `FailedPermanently`, `SkippedInvalidPayload` |

### 6.3 Enumerations

| Enum | Values |
|---|---|
| `NotificationType` | `Welcome`, `OrderConfirmation`, `PaymentReceipt`, `PaymentFailure`, `PaymentCancellation`, `RefundConfirmation`, `OrderCancellation`, `CheckoutPaymentFailure`, `ShippingUpdate`, `DeliveryConfirmation`, `LowStockAlert` |
| `DeliveryStatus` | `Pending`, `Sent`, `Failed`, `DeadLettered` |
| `ChannelType` | `Email` (used), `Sms`, `Push` (declared, unused) |
| `ProcessedOutcome` | `Sent`, `FailedPermanently`, `SkippedInvalidPayload` |

`Pending` is only held transiently inside the processing transaction; rows are committed
in their final state.

### 6.4 Migrations

Alembic (`alembic.ini`, `alembic/env.py`) with a single revision,
`20260605120000_initial_notifications`, creating both tables and their indexes. `env.py`
uses an async engine and takes the URL from `Settings.database_url`
(`DATABASE_URL`).

**Migrations are not run automatically** by the entrypoint, the API lifespan or the
worker. Run them manually before first use (see [10](#10-running-locally)).

---

## 7. Templates, SMTP and retry policy

### 7.1 Templates

Located in `app/templates/<slug>/`, each with `subject.j2`, `body.html.j2` and
`body.txt.j2`.

| Slug | Subject (rendered) |
|---|---|
| `welcome` | `Welcome to our store!` |
| `order_confirmation` | `Order {{ orderId }} confirmed` |
| `order_cancellation` | `Order {{ orderId }} cancelled` |
| `shipping_update` | `Order {{ orderId }} has shipped` |
| `delivery_confirmation` | `Order {{ orderId }} delivered` |
| `checkout_payment_failure` | `Checkout payment failed for order {{ orderId }}` |
| `payment_receipt` | `Payment receipt for order {{ orderId }}` |
| `payment_failure` | `Payment failed for order {{ orderId }}` |
| `payment_cancellation` | `Payment cancelled for order {{ orderId }}` |
| `refund_confirmation` | `Refund processed for order {{ orderId }}` |
| `low_stock_alert` | `Low stock alert: product {{ productId }}` |

`TemplateRenderer` details:

- Loader: `FileSystemLoader(app/templates)`.
- Missing variables render as an empty string (custom `_SilentUndefined`); `None` values
  in the context are replaced by `""`.
- The subject is stripped of surrounding whitespace. The text body is optional (empty if
  `body.txt.j2` fails to render).
- Templates are minimal, unstyled HTML fragments (no layout, no i18n).
- HTML autoescaping is configured with `select_autoescape(["html", "xml"])`, which keys
  on file extension. The template files end in `.html.j2`, so **autoescape is not active**
  (verified with `select_autoescape(...)("x/body.html.j2") == False`).

### 7.2 SMTP delivery (`EmailChannel`)

- Builds an `EmailMessage`: `From = SMTP_FROM`, `To = <recipient>`, plain-text part
  (falls back to HTML if the text body is empty) plus an HTML alternative.
- Sends with `aiosmtplib.send(...)`, timeout 30 s, one message per recipient.
- `SMTP_USE_TLS=true` -> implicit TLS; STARTTLS is used only when TLS is off **and** the
  port is `587`. `SMTP_USERNAME` / `SMTP_PASSWORD` are passed through (both optional).
- Error classification:

| Exception | Raised as | Effect |
|---|---|---|
| `SMTPRecipientsRefused`, `SMTPAuthenticationError` | `PermanentDeliveryError` | no retry |
| any other `SMTPException`, `OSError` (network, timeouts) | `TransientDeliveryError` | retried, then DLQ |

### 7.3 Retry policy

`NotificationDispatcher._send_with_retry` uses `tenacity`:

| Setting | Value |
|---|---|
| Retries only on | `TransientDeliveryError` |
| Max attempts | `MAX_DELIVERY_ATTEMPTS` (default `4`) |
| Wait | `wait_exponential_jitter(initial=1, max=10)` seconds |
| After exhaustion | publish to DLQ, `status=DeadLettered` |

Retries happen in-process while the message is unacked (the consumer holds up to 10
unacked messages via prefetch).

---

## 8. Configuration

Settings are read by `app/core/settings.py` (`pydantic-settings`) from environment
variables (case-insensitive, same name as the field) and an optional `.env` file in the
working directory. Unknown variables are ignored.

| Variable | Default | Description |
|---|---|---|
| `SERVICE_NAME` | `notifications-service` | OTel `service.name` |
| `LOG_LEVEL` | `INFO` | Log level |
| `DATABASE_URL` | `postgresql+asyncpg://postgres:postgres@localhost:5433/notifications` | SQLAlchemy async URL |
| `RABBITMQ_URL` | `amqp://guest:guest@localhost:5672/` | AMQP connection URL |
| `RABBITMQ_QUEUE` | `notifications.events` | Work queue |
| `RABBITMQ_DLX` | `notifications-dlx` | Dead-letter exchange (direct) |
| `RABBITMQ_DLQ` | `notifications.dlq` | Dead-letter queue and routing key |
| `SMTP_HOST` | `localhost` | SMTP host |
| `SMTP_PORT` | `1025` | SMTP port |
| `SMTP_FROM` | `noreply@ecommerce.local` | From address |
| `SMTP_USE_TLS` | `false` | Implicit TLS |
| `SMTP_USERNAME` | unset | SMTP user |
| `SMTP_PASSWORD` | unset | SMTP password |
| `NOTIFICATIONS_OPERATOR_EMAILS` | `ops@example.com` | Comma-separated operator recipients for low-stock alerts |
| `OTEL_ENABLED` | `true` | Enable tracing |
| `OTEL_ENDPOINT` | `http://localhost:4317` | OTLP gRPC endpoint (insecure) |
| `MAX_DELIVERY_ATTEMPTS` | `4` | SMTP attempts per recipient before DLQ |
| `SERVICE_MODE` | `api` | Read by `docker/entrypoint.sh` only: `api`, `worker` or `both` |

**Docker Compose variables** (`docker/template.env`, copy to `docker/.env`):

| Variable | Template value | Used for |
|---|---|---|
| `NOTIFICATIONS_API_PORT` | `8200` | Host port -> container `8200` |
| `NOTIFICATIONS_LOG_LEVEL` | `INFO` | `LOG_LEVEL` |
| `NOTIFICATIONS_OPERATOR_EMAILS` | `ops@example.com,inventory@example.com` | Operator recipients |
| `MAILHOG_SMTP_PORT` | `1025` | Host port -> MailHog `1025` |
| `MAILHOG_UI_PORT` | `8025` | Host port -> MailHog `8025` |
| `POSTGRES_PASSWORD`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD` | (set in `.env`) | Interpolated into `DATABASE_URL` / `RABBITMQ_URL` |

The compose entries set `SMTP_HOST=mailhog`, `SMTP_PORT=1025`, `SMTP_USE_TLS=false`,
`SMTP_FROM=noreply@ecommerce.local`, `OTEL_ENDPOINT=http://jaeger:4317`,
`OTEL_ENABLED=true` and `RABBITMQ_QUEUE=notifications.events`. `SERVICE_NAME` is
`notifications-service` for the API and `notifications-worker` for the worker.
The compose `DATABASE_URL` hard-codes the DB user `postgres` (it does not use
`POSTGRES_USER`).

---

## 9. Observability

| Aspect | Behaviour |
|---|---|
| Logging | `structlog` with `ConsoleRenderer` (human-readable, **not** JSON) to stdout; level from `LOG_LEVEL`. The worker binds `correlation_id` into the log context per message. |
| Tracing | When `OTEL_ENABLED=true`: OTLP/gRPC exporter to `OTEL_ENDPOINT` (Jaeger UI at `http://localhost:16686` in the compose stack), `BatchSpanProcessor`, resource `service.name = SERVICE_NAME`. Instruments FastAPI (the `/health` URL is excluded), httpx, aio-pika and stdlib logging. The worker enables the same without FastAPI. |
| Log/trace correlation | A structlog processor adds `trace_id` and `span_id` to log events when a span is recording. |
| Health | `GET /health` (see [4.1](#41-get-health)). The compose file defines no container healthcheck for the service. |
| Audit trail | The `notifications` table, queryable through the history API. |

---

## 10. Running locally

### 10.1 Docker Compose (recommended)

Requires Docker. Compose project name is `ecommerce-infra`.

```bash
cd docker
cp template.env .env        # then fill in POSTGRES/RABBITMQ credentials as needed

docker compose up -d --build postgres rabbitmq mailhog jaeger notifications-api notifications-worker

# Create the tables (not done automatically):
docker compose exec notifications-api alembic upgrade head
```

If the Postgres volume already existed before `postgres-init/notifications.sql` was added,
create the database by hand:

```bash
docker compose exec postgres psql -U postgres -c "CREATE DATABASE notifications;"
```

Endpoints (default host ports):

| Service | URL / port |
|---|---|
| Notifications API | `http://localhost:8200` (`/health`, `/notifications`, `/docs`) |
| Through gateway | `http://localhost:5133/notifications` (requires `api-gateway`) |
| MailHog web UI | `http://localhost:8025` |
| MailHog SMTP | `localhost:1025` |
| RabbitMQ management | `http://localhost:15672` |
| Jaeger UI | `http://localhost:16686` |
| PostgreSQL (host) | `localhost:5433` (`POSTGRES_PORT`), database `notifications` |

The `notifications-worker` container exposes no port.

`docker/entrypoint.sh` selects the process via `SERVICE_MODE`:
`api` (uvicorn on `0.0.0.0:8200`), `worker` (`python -m app.workers.consumer`), or `both`
(worker in the background plus uvicorn; the background worker is not supervised). The
Dockerfile `CMD` starts only uvicorn; compose overrides it with the entrypoint script.

### 10.2 MailHog

MailHog (`mailhog/mailhog:latest`) accepts any SMTP message on port `1025` without
authentication and shows it in the web UI at `http://localhost:8025` (HTTP API:
`http://localhost:8025/api/v2/messages`). Each notification appears as a multipart
(text + HTML) message from `noreply@ecommerce.local`.

### 10.3 Smoke test without the other services

Open the RabbitMQ management UI (`http://localhost:15672`) -> Exchanges -> `ecommerce.events`
(declared by the worker) -> Publish message, routing key `user.registered`, payload:

```json
{ "userId": "9b8f6c1e-5f0a-4c53-9b0f-1c2d3e4f5a6b", "email": "me@example.com", "role": "Customer" }
```

Then check MailHog and:

```bash
curl "http://localhost:8200/notifications?eventType=user.registered"
```

Publishing the same `userId` again results in no second email (idempotency).

### 10.4 Native (without Docker for the app)

Start the infrastructure (Postgres, RabbitMQ, MailHog, optionally Jaeger) with compose,
then:

```powershell
cd Notifications
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e ".[dev]"

# Defaults already point at localhost:5433 (Postgres), localhost:5672 (RabbitMQ),
# localhost:1025 (SMTP) and localhost:4317 (OTLP). Override as needed, e.g.:
$env:RABBITMQ_URL = "amqp://<user>:<password>@localhost:5672/"
$env:OTEL_ENABLED = "false"          # if Jaeger is not running

alembic upgrade head

uvicorn app.api.main:app --host 0.0.0.0 --port 8200 --reload   # API
python -m app.workers.consumer                                 # worker (second terminal)
```

Use the same variables in a `.env` file in `Notifications/` if you prefer.

---

## 11. Tests

```bash
cd Notifications
pip install -e ".[dev]"
pytest                      # pytest-asyncio in auto mode, testpaths = tests
ruff check .                # lint (line length 100)
```

The suite (12 tests) needs **no** Postgres, RabbitMQ or SMTP server: it uses an in-memory
SQLite database via `aiosqlite` (`Base.metadata.create_all`), a mocked delivery channel
and a mocked DLX exchange.

| File | Covers |
|---|---|
| `tests/unit/test_handlers.py` | `user.registered` skip on missing email; `order.created` line-summary formatting |
| `tests/unit/test_idempotency_service.py` | duplicate detection; duplicate insert returns `False` |
| `tests/unit/test_notification_dispatcher.py` | retry exhaustion routes to DLQ |
| `tests/unit/test_template_renderer.py` | safe defaults for missing fields; line summary rendering |
| `tests/integration/test_welcome_flow.py` | welcome email dispatched once; redelivery is idempotent |
| `tests/integration/test_idempotency.py` | duplicate event yields a single `notifications` row |
| `tests/integration/test_retry_dlq.py` | transient failures end as `DeadLettered`, `attempt_count == 4`, one DLQ publish |
| `tests/integration/test_history_api.py` | `GET /notifications` filtering by `eventType` |

Only the `user.registered` and `order.created` handlers are exercised directly; the
consumer (`EventConsumer`), SMTP channel, health checks, `GET /notifications/{id}` and
the other handlers have no tests. The retry tests use the real tenacity backoff
(1 s initial, jittered), so they take a few seconds.

---

## 12. Known gaps and caveats

- **No automatic migrations.** Tables are only created by `alembic upgrade head`. Until
  then the API's list endpoint fails and the worker cannot process events (each message is
  requeued repeatedly).
- **Binding mismatch to verify:** this service binds `order.payment.failed`, whereas the
  Orders code publishes its payment-failure event with routing key `order.payment_failed`
  (underscore). If that is still the case, `checkout_payment_failure` emails will never be
  triggered. Customers still receive the `payment.failed` email from `payments-exchange`.
- `order.shipped` and `order.delivered` are consumed here, but the Orders service code I
  reviewed for this README does not appear to publish them yet.
- **Poison messages loop.** Unexpected exceptions, including Pydantic validation errors
  from missing required fields (for example no `orderId`), use `nack(requeue=True)` with no
  retry limit or delay. Only invalid JSON is dead-lettered.
- **Possible duplicate email on crash.** The idempotency record is committed after the SMTP
  send. A crash between the two causes redelivery and a second email.
- `attempt_count` is approximate: it is `1` for any successful send (even after retries)
  and `MAX_DELIVERY_ATTEMPTS` for any failure.
- `inventory.depleted` is keyed by `productId` only, so only the **first** depletion of a
  product ever triggers an alert. The multi-recipient `recipient` column is `varchar(320)`;
  a long operator list can exceed it.
- Operator alert delivery is sequential: a failure on one recipient aborts the rest, even
  if earlier recipients were already emailed.
- DLQ messages carry the template context, not the original event, and there is no replay
  tooling.
- `GET /health` always returns HTTP 200 (`degraded` when any dependency fails), which is
  not suitable as a status-code-based readiness probe.
- The history API and its gateway route are unauthenticated and expose recipient
  addresses.
- Index drift: the ORM declares `created_at DESC` for the two composite indexes, while the
  migration creates them ascending.
- `RABBITMQ_URL` (including credentials) is logged at INFO level by the worker on startup.
- Templates are unstyled, English-only, and not HTML-autoescaped (see [7.1](#71-templates)).
  `isFullRefund`, `totalRefunded`, `originalAmount` and `itemCount` are accepted but not
  shown in emails.
- The root `README.md` links to `specs/002-notifications-service/quickstart.md`; there is
  no `specs/` directory in the repository, so this README is the authoritative reference.
- `Sms` / `Push` channels exist only as enum values.
