# Auth Service

Identity and authentication microservice of the 3D-printing e-commerce platform. It registers users, authenticates them with a local email/password or a Google ID token, issues short-lived JWT access tokens and rotating refresh tokens, and announces user lifecycle events over RabbitMQ.

- Service name (OpenTelemetry): `AuthService`
- Runtime: ASP.NET Core minimal APIs on .NET 10
- Datastore: PostgreSQL (EF Core). No Redis, MinIO, Qdrant or Neo4j.
- Messaging: publishes to a RabbitMQ topic exchange. It does not consume anything.

## Table of contents

1. [Role in the system](#1-role-in-the-system)
2. [Tech stack](#2-tech-stack)
3. [Project layout](#3-project-layout)
4. [HTTP API](#4-http-api)
5. [Messaging (RabbitMQ)](#5-messaging-rabbitmq)
6. [Data storage and security model](#6-data-storage-and-security-model)
7. [Domain model and business rules](#7-domain-model-and-business-rules)
8. [Configuration](#8-configuration)
9. [Observability](#9-observability)
10. [Running locally](#10-running-locally)
11. [Testing](#11-testing)
12. [Notes, known gaps and inconsistencies](#12-notes-known-gaps-and-inconsistencies)

---

## 1. Role in the system

```
Client ──> API Gateway (YARP, /auth/{**catch-all}) ──> Auth (http://auth:8080)
                                                         │
                                  PostgreSQL (Users, RefreshTokens)
                                                         │
                                  RabbitMQ  exchange "ecommerce.events" (topic)
                                       ├── user.registered ──> Notifications (welcome email)
                                       └── user.logged_in  ──> (no consumer found in this repo)
```

| Relationship | Details |
|---|---|
| **API Gateway** | The gateway proxies every `/auth/{**catch-all}` request to `http://auth:8080` (route `auth-public`, cluster `auth-cluster`). The gateway validates the JWTs that Auth issues using the same `Issuer`, `Audience` and HMAC key (its setting is named `Jwt:Secret`; Auth calls it `Jwt:Key`). The gateway forwards `X-User-Id` (from `sub`) and `X-User-Email` (from `email`) to downstream services. |
| **Notifications** | Consumes `user.registered` from exchange `ecommerce.events` to send a welcome email (Python service, handler `UserRegisteredHandler`). |
| **Other services** | Do not call Auth directly. They rely on the JWT validated at the gateway. |
| **Google** | `POST /auth/google` validates a Google ID token with `Google.Apis.Auth` (`GoogleJsonWebSignature.ValidateAsync`, audience = `GoogleAuth:ClientId`). This makes an outbound call to Google to fetch signing certificates. |

Auth itself exposes **no protected endpoint**. It configures JWT bearer authentication and authorization, but no route requires them.

## 2. Tech stack

| Area | Technology |
|---|---|
| Language / runtime | C# (`Nullable` and `ImplicitUsings` enabled), .NET 10 (`net10.0`), base image `mcr.microsoft.com/dotnet/aspnet:10.0` |
| Web | ASP.NET Core minimal APIs (`Microsoft.NET.Sdk.Web`) |
| Persistence | EF Core 10.0.6, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1, PostgreSQL 17 (compose image) |
| Passwords and token hashing | `BCrypt.Net-Next` 4.1.0 |
| JWT | `System.IdentityModel.Tokens.Jwt` 8.17.0 (issuing), `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.6 (validation config) |
| Google sign-in | `Google.Apis.Auth` 1.73.0 |
| Messaging | `RabbitMQ.Client` 7.2.1 |
| Health checks | `AspNetCore.HealthChecks.NpgSql` 9.0.0, `AspNetCore.HealthChecks.Rabbitmq` 9.0.0, `AspNetCore.HealthChecks.UI.Client` 9.0.0 (UI.Client is referenced but not used) |
| Observability | `OpenTelemetry.Extensions.Hosting` 1.15.2, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.15.2, `Instrumentation.AspNetCore` 1.15.1, `Instrumentation.Http` 1.15.0, `Instrumentation.EntityFrameworkCore` 1.15.0-beta.1, `Npgsql.OpenTelemetry` 10.0.2 (referenced, not wired) |
| Other | `Microsoft.AspNetCore.OpenApi` 10.0.3 and `Microsoft.EntityFrameworkCore.InMemory` 10.0.6 are referenced in `Auth.csproj` but not used in code |
| Tests | xUnit 2.9.3, Moq 4.20.72, FluentAssertions 8.9.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.6, Testcontainers 4.11.0 (PostgreSql, RabbitMq) |

## 3. Project layout

The solution `Auth.sln` is a layered (clean-architecture style) set of projects. Note that `Application` references `Infrastructure` directly (`RefreshTokenHandler` uses `AuthDbContext`).

```
Auth/
├── Auth.sln
├── .dockerignore
├── Auth/                      # Web host (composition root)
│   ├── Program.cs             # DI, auth, OTel, health checks, middleware pipeline
│   ├── Endpoints/             # Register/Login/RefreshToken/GoogleLogin endpoint mappers
│   ├── Extensions/            # MapAuthEndpoints(), UseGlobalExceptionHandling()
│   ├── Middleware/            # ExceptionMiddleware, CorrelationIdMiddleware, RequestLoggingMiddleware
│   ├── appsettings.json, appsettings.Development.json
│   ├── Properties/launchSettings.json
│   ├── Auth.http              # Manual request samples
│   └── Dockerfile
├── Application/Features/      # One folder per use case: Command record + Handler class
│   ├── Register/  Login/  RefreshToken/  GoogleLogin/
├── Domain/
│   ├── Entities/              # User, RefreshToken
│   ├── Abstractions/          # IUserRepository, IJwtProvider, IRefreshTokenService,
│   │                          # IGoogleTokenValidator (+ GoogleUserInfo), IEventPublisher
│   └── Exceptions/            # AppException, InvalidCredentialsException
├── Infrastructure/
│   ├── Persistence/           # AuthDbContext, Configurations/, Repositories/UserRepository
│   ├── Migrations/            # EF Core migrations (5) + model snapshot
│   ├── Authentication/        # JwtProvider, RefreshTokenService
│   ├── External/Google/       # GoogleTokenValidator
│   └── Messaging/             # RabbitMqPublisher, NoOpEventPublisher
├── Contracts/                 # Events/UserRegisteredEvent, UserLoggedInEvent; Responses/ErrorResponse
├── UnitTests/                 # xUnit + Moq handler tests
└── IntegrationTests/          # WebApplicationFactory + Testcontainers
```

Request pipeline order (`Program.cs`): `UseGlobalExceptionHandling()` (adds `ExceptionMiddleware`), a lambda that sets `HttpContext.TraceIdentifier` to a new GUID, `ExceptionMiddleware` (registered a second time), `CorrelationIdMiddleware`, `RequestLoggingMiddleware`, `UseAuthentication`, `UseAuthorization`, then the endpoints and `/health`.

## 4. HTTP API

Base URL: `http://localhost:5132` (docker compose or `dotnet run`), `http://auth:8080` inside the compose network, or through the gateway. All endpoints are `POST`, require **no authentication**, accept and return `application/json`, and have no role requirements. Property names in request bodies are matched case-insensitively (ASP.NET Core default); success responses are camelCase.

| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | `/auth/register` | none | Create a local user |
| POST | `/auth/login` | none | Email/password login |
| POST | `/auth/refresh` | none | Rotate a refresh token and get a new access token |
| POST | `/auth/google` | none | Login or sign-up with a Google ID token |
| GET | `/health` | none | Health check (not mapped in the `Testing` environment) |

There is **no** `/auth/me`, logout, password-reset, email-verification or role-management endpoint (`Auth.http` contains a `GET /auth/me` sample that has no matching route).

### POST `/auth/register`

Request (`RegisterCommand`):

```json
{ "email": "test@example.com", "password": "Password123!" }
```

Response `200 OK`: the new user id as a bare JSON string.

```json
"3f2b8c1e-5a47-4d0e-9c1a-0e6f4c7a9b21"
```

Behavior: looks up the email by `NormalizedEmail`; if it exists the handler throws (see error handling below). Otherwise it stores the user (`Role = "Customer"`, `Provider = "Local"`, bcrypt password hash), then publishes `user.registered`. Registration does **not** return tokens; call `/auth/login` afterwards. There is no email-format or password-strength validation.

### POST `/auth/login`

Request (`LoginCommand`):

```json
{ "email": "test@example.com", "password": "Password123!" }
```

Response `200 OK`:

```json
{ "accessToken": "<jwt>", "refreshToken": "kSZ7mYm+50qcDCED3oyXmQ==" }
```

Behavior: finds the user by normalized email, verifies the bcrypt hash, creates a JWT and a new refresh token (new token family), persists the refresh token, then publishes `user.logged_in`. An unknown email or wrong password throws `Exception("Invalid credentials")`.

### POST `/auth/refresh`

Request (`RefreshTokenCommand`):

```json
{ "refreshToken": "kSZ7mYm+50qcDCED3oyXmQ==" }
```

Response `200 OK`: same shape as login (`accessToken`, `refreshToken`). The old token is revoked and replaced (rotation). See [section 6.4](#64-refresh-tokens) for the algorithm.

Failure cases (all plain `Exception`): `"Invalid refresh token"` (no match among non-expired tokens) and `"Token reuse detected. Session revoked."` (a revoked token was presented; the whole family is revoked).

### POST `/auth/google`

Request (`GoogleLoginCommand`):

```json
{ "idToken": "<google id token>" }
```

Response `200 OK`: same shape as login. Behavior: validate the ID token (audience `GoogleAuth:ClientId`), lower-case the email, find the user; create a `Provider = "Google"` user with `Role = "Customer"` if absent; if an existing user has `Provider == "Local"` it is switched to `Google` and the Google subject is stored in `ProviderId` (the password hash is kept, so password login still works). Then issue a JWT and refresh token and publish `user.logged_in`. Note that no `user.registered` event is published for Google sign-ups (see section 12 for a probable problem with creating new Google users).

### GET `/health`

Runs the PostgreSQL (Npgsql) and RabbitMQ health checks and returns JSON (`200` when healthy; ASP.NET Core's default mapping returns `503` when unhealthy):

```json
{ "status": "Healthy", "checks": [ { "name": "npgsql", "status": "Healthy", "error": null },
                                    { "name": "rabbitmq", "status": "Healthy", "error": null } ] }
```

### Error handling and status codes

`ExceptionMiddleware` converts exceptions to an `ErrorResponse` (`Contracts/Responses/ErrorResponse.cs`). It is serialized with default `System.Text.Json` options, so the properties are **PascalCase**:

```json
{ "Message": "An unexpected error occurred.", "Code": "server.internal_error",
  "TraceId": "b3a1f0c2-...", "Details": null }
```

| Status | When |
|---|---|
| `200` | Success on all four POST endpoints |
| `400` | A thrown `AppException` (uses its `Code`, e.g. `auth.invalid_credentials`). **No code path throws an `AppException` today**, because `InvalidCredentialsException` is defined but never used. A request body that cannot be bound to the command may also yield `400` from the framework (in the `Development` environment the framework throws instead, which the middleware turns into `500`). |
| `500` | Every other exception, including all business failures: duplicate registration, invalid credentials, invalid or reused refresh token, invalid Google token, database or RabbitMQ failures. `Code` is `server.internal_error`. Outside `Development` the message is generic; in `Development` it is the exception message and `Details.stackTrace` holds the stack trace. |

Consequently clients cannot distinguish "wrong password" from a server fault by status code. See section 12.

## 5. Messaging (RabbitMQ)

### Publishing

`RabbitMqPublisher` (singleton `IEventPublisher`) opens a connection and channel lazily on the first publish and declares the exchange.

| Property | Value |
|---|---|
| Exchange | `ecommerce.events` (setting `RabbitMq:Exchange`) |
| Exchange type | `topic`, `durable: true` |
| Virtual host | default (`/`); not configurable |
| Queues | None declared or bound by Auth. Consumers declare their own queues. |
| Message properties | `Persistent = true` (delivery mode 2); `mandatory: false`; no headers, message id or correlation id |
| Serialization | `System.Text.Json.JsonSerializer.Serialize(event)` with default options, so property names are **PascalCase** |
| Publisher confirms | Not used |

| Routing key | Event (`Contracts/Events`) | Published by |
|---|---|---|
| `user.registered` | `UserRegisteredEvent` | `RegisterHandler` (after the user row is saved) |
| `user.logged_in` | `UserLoggedInEvent` | `LoginHandler` and `GoogleLoginHandler` (after the refresh token is saved) |

#### `user.registered` payload

| Field | Type | Notes |
|---|---|---|
| `UserId` | GUID string | New user id |
| `Email` | string | Lower-cased email |
| `Role` | string | Always `"Customer"` at registration |

```json
{ "UserId": "3f2b8c1e-5a47-4d0e-9c1a-0e6f4c7a9b21", "Email": "test@example.com", "Role": "Customer" }
```

#### `user.logged_in` payload

| Field | Type |
|---|---|
| `UserId` | GUID string |
| `Email` | string |

```json
{ "UserId": "3f2b8c1e-5a47-4d0e-9c1a-0e6f4c7a9b21", "Email": "test@example.com" }
```

Publishing characteristics:

- Publishing happens synchronously inside the request, **after** the database commit. If RabbitMQ is unavailable the request fails with `500` even though the user or refresh token has already been saved. There is no outbox, retry or dead-lettering on the publisher side.
- The connection is never re-created after a failure (it is only cached), and the shared channel is not synchronized for concurrent use.
- In the `Testing` environment `NoOpEventPublisher` replaces the publisher and nothing is sent.
- The `Notifications` consumer converts the first letter of every key to lower case (`normalize_wire_payload`) before validating with Pydantic (`userId`, `email`, `role`), so the PascalCase payloads are accepted. Keep this in mind when changing contract field names.

### Consuming

Auth consumes no queues. (Consumer-side behavior such as idempotency and retry is the responsibility of subscribers like `Notifications`, which binds `user.registered` on `ecommerce.events`.)

## 6. Data storage and security model

### 6.1 PostgreSQL

One database (`ecommerce` in the compose setup, shared by default connection string), default schema, EF Core migrations history table `__EFMigrationsHistory`. Identity columns are not used; primary keys are application-generated GUIDs. Enumerations are not used; `Role` and `Provider` are plain strings.

**Table `Users`**

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK (`PK_Users`), `Guid.NewGuid()` |
| `Email` | `varchar(256)` | no | Unique index `IX_Users_Email`. Stored lower-cased (`UserRepository.AddAsync`). |
| `NormalizedEmail` | `text` | no | Unique index `IX_Users_NormalizedEmail`. `Trim().ToLowerInvariant()` of the email; used for lookups. |
| `PasswordHash` | `varchar(500)` | no | bcrypt hash. Column is NOT NULL (see section 12 regarding Google users). |
| `Role` | `varchar(50)` | no | String, default value in code `"Customer"` |
| `CreatedAt` | `timestamptz` | no | UTC |
| `Provider` | `varchar(50)` | no | `"Local"` or `"Google"` |
| `ProviderId` | `varchar(200)` | yes | Google `sub`; unique composite index `IX_Users_Provider_ProviderId` on (`Provider`, `ProviderId`) |

**Table `RefreshTokens`**

| Column | Type | Null | Notes |
|---|---|---|---|
| `Id` | `uuid` | no | PK (`PK_RefreshTokens`) |
| `UserId` | `uuid` | no | FK `FK_RefreshTokens_Users_UserId` to `Users(Id)`, `ON DELETE CASCADE`; index `IX_RefreshTokens_UserId` |
| `TokenHash` | `text` | no | bcrypt hash of the raw token (the raw token is never stored) |
| `FamilyId` | `uuid` | no | Rotation chain id; index `IX_RefreshTokens_FamilyId` |
| `ReplacedByTokenId` | `uuid` | yes | Id of the successor token (no FK, no index) |
| `IsRevoked` | `boolean` | no | |
| `CreatedAt` | `timestamptz` | no | UTC |
| `ExpiresAt` | `timestamptz` | no | `CreatedAt + 7 days` |

Migrations (`Infrastructure/Migrations`): `InitialCreate`, `RefreshTokens`, `TokenReplay`, `GoogleLogin`, `AddNormalizedEmail` (dated 2026-04-20 to 2026-04-21). Existing rows added by `AddNormalizedEmail` get `NormalizedEmail = ''` by the column default, which would collide with the new unique index if more than one pre-existing user existed.

**When migrations run:** `db.Database.Migrate()` is executed at startup only when `ASPNETCORE_ENVIRONMENT=Development` (and not `Testing`). In any other environment no migration is applied automatically; run them yourself (for example `dotnet ef database update --project Infrastructure --startup-project Auth`).

### 6.2 Access tokens (JWT)

Created by `JwtProvider`.

| Item | Value |
|---|---|
| Algorithm | HMAC-SHA256 (`HS256`), symmetric key from `Jwt:Key` (UTF-8 bytes) |
| Lifetime | 60 minutes (`expires = UtcNow + 60 min`), not configurable |
| `iss` / `aud` | `Jwt:Issuer` (`auth-service`) / `Jwt:Audience` (`ecommerce`) |
| Claims | `sub` = user id (GUID), `email` = user email, role claim of type `ClaimTypes.Role` = user role (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) |
| Not included | `jti`, `iat`, `nbf` |

Validation configured in Program.cs: issuer, audience and signing key are validated (lifetime validation is on by default). Consumers must use the same key, issuer and audience.

### 6.3 Passwords

`BCrypt.Net.BCrypt.HashPassword(password)` with library defaults (random salt embedded in the hash, work factor 11), verified with `BCrypt.Verify`. No pepper, no password policy, no lockout, no rate limiting inside the service (the gateway defines rate limiters in its own configuration).

### 6.4 Refresh tokens

- **Raw token:** `Base64(Guid.NewGuid().ToByteArray())` (a 24-character string), returned to the client once. Stored only as a bcrypt hash (`TokenHash`).
- **Lifetime:** 7 days from creation.
- **Family:** each login (local or Google) starts a new `FamilyId`. Refresh keeps the family of the presented token.
- **Rotation (`RefreshTokenHandler`):**
  1. Load all refresh tokens with `ExpiresAt > now` (with their `User`) and find the one whose hash matches the presented value (`BCrypt.Verify` against each candidate).
  2. No match: error `"Invalid refresh token"`.
  3. Match but `IsRevoked`: **reuse detection**. All non-revoked tokens of the family are revoked and the request fails with `"Token reuse detected. Session revoked."`.
  4. Otherwise mark the token revoked, create a successor in the same family, set `ReplacedByTokenId`, save, and return a new JWT (built from the user's current email and role) and the new raw token.
- Expired and revoked rows are never deleted (no cleanup job).

### 6.5 Roles

`User.Role` is a free-form string. The code only ever assigns `"Customer"` (register and Google sign-up). There is no API to change a role; any other role (for example an administrator) must be set directly in the database. The role is copied into the JWT at issue time.

## 7. Domain model and business rules

- **User** (`Domain/Entities/User.cs`): `Id`, `Email`, `NormalizedEmail`, `PasswordHash`, `Role`, `CreatedAt`, `Provider`, `ProviderId`, `RefreshTokens`.
- **RefreshToken** (`Domain/Entities/RefreshToken.cs`): `Id`, `UserId`, `TokenHash`, `FamilyId`, `ReplacedByTokenId`, `IsRevoked`, `CreatedAt`, `ExpiresAt`, computed `IsExpired`.
- Email uniqueness is enforced case-insensitively through `NormalizedEmail` (application check plus unique indexes).
- Refresh-token lifecycle: `active` -> `revoked` (on rotation) -> reuse triggers family revocation.
- Account linking: logging in with Google using an email that exists as a `Local` account converts it to a `Google` account.
- Handlers (`RegisterHandler`, `LoginHandler`, `RefreshTokenHandler`, `GoogleLoginHandler`) are plain scoped classes called directly from the endpoints (no mediator, no validation pipeline).
- Background jobs: none.

## 8. Configuration

Standard ASP.NET Core configuration. Environment variables use `__` as the section separator (for example `ConnectionStrings__Default`, `Jwt__Key`). Values in `appsettings*.json` are development defaults; secrets must be overridden for any real deployment.

| Key / variable | Default (`appsettings.json`) | Meaning |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` in `launchSettings.json` and compose | `Development` enables auto-migration and detailed error messages. `Testing` swaps in `NoOpEventPublisher` and disables health checks. |
| `ASPNETCORE_URLS` | `http://+:8080` in compose; `http://localhost:5132` via launch profile | Listen addresses |
| `ConnectionStrings:Default` | `Host=postgres;Port=5432;Database=ecommerce;Username=postgres;...` (dev credentials). `appsettings.Development.json` uses `Port=5433`. | Npgsql connection string |
| `Jwt:Key` | A development placeholder string committed in `appsettings*.json` | HMAC signing key. Must match the gateway's `Jwt:Secret`. Override in production. |
| `Jwt:Issuer` | `auth-service` | `iss` claim and validation |
| `Jwt:Audience` | `ecommerce` | `aud` claim and validation |
| `GoogleAuth:ClientId` | `YOUR_CLIENT_ID` (placeholder) | Expected audience of Google ID tokens |
| `GoogleAuth:ClientSecret` | `YOUR_SECRET` (placeholder) | Present in config but not read by the code |
| `RabbitMq:Host` | `rabbitmq` | Broker host |
| `RabbitMq:Port` | `5672` | Broker AMQP port |
| `RabbitMq:Username` / `RabbitMq:Password` | `guest` / `guest` | Broker credentials |
| `RabbitMq:Exchange` | `ecommerce.events` | Topic exchange to publish to |
| `OtelExportUri` | `http://jaeger:4317` | OTLP (gRPC) endpoint for traces and logs. Tracing throws at startup if it resolves to an empty string. |

Compose (`docker/docker-compose.yml`, service `auth`) only sets `ASPNETCORE_ENVIRONMENT=Development` and `ASPNETCORE_URLS=http://+:8080`; everything else comes from `appsettings.Development.json` (see section 12). Compose variable: `AUTH_PORT` (default `5132` in `docker/template.env`) is the host port.

## 9. Observability

- **Tracing:** OpenTelemetry with resource service name `AuthService`; ASP.NET Core, HttpClient and EF Core instrumentation; OTLP exporter to `OtelExportUri` (Jaeger all-in-one, UI on `OTEL_UI_PORT`, default `16686`, in compose). `RabbitMqPublisher` creates an `ActivitySource` named `AuthService.RabbitMQ` with spans `RabbitMQ Publish`, but that source is not registered with `AddSource`, so those spans are not exported. `Npgsql.OpenTelemetry` is referenced but `AddNpgsql()` is not called.
- **Logging:** console provider plus the OpenTelemetry logging provider (scopes, formatted messages, OTLP export to `OtelExportUri`). Endpoints log attempts and successes; login and registration logs include the email address. `RequestLoggingMiddleware` logs `HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms`.
- **Correlation id:** `CorrelationIdMiddleware` reads or generates `X-Correlation-Id`, stores it in `HttpContext.Items` and echoes it in the response header. It is not added to log scopes or published events. The gateway adds the header via `AddCorrelationIdTransform`.
- **Trace id in errors:** `HttpContext.TraceIdentifier` is overwritten with a random GUID per request and returned as `TraceId` in error bodies.
- **Health:** `GET /health` (see section 4); checks PostgreSQL via the connection string and RabbitMQ via a new connection per check. Used by the gateway integration-test fixture to wait for readiness. The compose file defines no container healthcheck for Auth.

## 10. Running locally

### Docker Compose (recommended)

From the repository root:

```powershell
cd docker
copy template.env .env      # then fill the empty values (RabbitMQ, MinIO, Neo4j, ...)
docker compose up -d --build postgres rabbitmq jaeger auth
```

| Item | Value |
|---|---|
| Container name | `auth` (image `ecommerce-infra-auth:latest`, compose project `ecommerce-infra`) |
| Container port | `8080` |
| Host port | `${AUTH_PORT}` = `5132` |
| Depends on | `postgres`, `rabbitmq`, `jaeger` (no health-based ordering) |

Build context is `Auth/` and the Dockerfile is `Auth/Auth/Dockerfile` (multi-stage: restore with all project files, build, publish, run `dotnet Auth.dll` on `aspnet:10.0`).

Important: the compose service does not override `ConnectionStrings__Default`, so in the `Development` environment the app uses `Port=5433` from `appsettings.Development.json`. Inside the compose network PostgreSQL listens on `5432`; add an override such as `ConnectionStrings__Default: "Host=postgres;Port=5432;Database=ecommerce;Username=...;Password=..."` if the container cannot reach the database. The gateway's `AuthFixture` test sets exactly this override. Likewise, if `RABBITMQ_USER` / `RABBITMQ_PASSWORD` in `.env` differ from `guest`/`guest`, set `RabbitMq__Username` and `RabbitMq__Password` for the service.

Quick check:

```powershell
curl http://localhost:5132/health
curl -X POST http://localhost:5132/auth/register -H "Content-Type: application/json" -d '{"email":"test@example.com","password":"Password123!"}'
curl -X POST http://localhost:5132/auth/login    -H "Content-Type: application/json" -d '{"email":"test@example.com","password":"Password123!"}'
```

`Auth/Auth/Auth.http` contains the same requests for the VS Code REST Client / Rider.

### Native (`dotnet run`)

Requires the .NET 10 SDK, a reachable PostgreSQL and RabbitMQ (for example the compose `postgres` published on `localhost:5433` and `rabbitmq` on `5672`).

```powershell
cd Auth
$env:ConnectionStrings__Default = "Host=localhost;Port=5433;Database=ecommerce;Username=postgres;Password=<password>"
$env:RabbitMq__Host   = "localhost"
$env:OtelExportUri    = "http://localhost:4317"
dotnet run --project Auth --launch-profile http     # http://localhost:5132 (https profile: https://localhost:7167)
```

The default hostnames in `appsettings*.json` (`postgres`, `rabbitmq`, `jaeger`) only resolve inside the compose network, hence the overrides. The `Development` environment applies migrations automatically at startup.

## 11. Testing

```powershell
cd Auth
dotnet test UnitTests          # no external dependencies
dotnet test IntegrationTests   # requires a running Docker daemon
dotnet test                    # whole solution
```

| Project | Contents | Needs |
|---|---|---|
| `UnitTests` | `LoginHandlerTests` (valid login publishes `user.logged_in`; unknown user throws `Exception`), `GoogleLoginHandlerTests` (creates a user when missing). Uses Moq mocks from `TestFixtures`. 3 tests, all passing at the time of writing. | Nothing |
| `IntegrationTests` | `AuthFlowTests`: register then login returns tokens; refresh returns success. Uses `WebApplicationFactory<Program>` with environment `Testing` (no-op publisher, no health checks), and a Testcontainers PostgreSQL 17 (`auth_test` database) whose schema is rebuilt with `EnsureDeleted` and `Migrate` per test class. | Docker. The shared fixture also starts a `rabbitmq:3-management` container, although the no-op publisher means RabbitMQ is not used. |

Coverage gaps: no tests for refresh rotation and reuse detection, duplicate registration, wrong password with a real database, invalid Google tokens, or the error middleware.

## 12. Notes, known gaps and inconsistencies

Observed directly from the code:

1. **Business errors return HTTP 500.** Handlers throw plain `Exception`, which `ExceptionMiddleware` maps to `500 server.internal_error`. `InvalidCredentialsException` and the `400` path exist but are unused. Wrong password, unknown user, duplicate email, bad or reused refresh token and bad Google token are indistinguishable by status code.
2. **Google sign-up of a new user probably fails.** `GoogleLoginHandler` creates a `User` without `PasswordHash` (null) while the `Users.PasswordHash` column is `NOT NULL` in the migrations and model snapshot. The unit test uses a mocked repository, so it does not catch this. Conversely, password login for a Google-only account would call `BCrypt.Verify` with a null hash.
3. **Refresh does a full scan.** `RefreshTokenHandler` loads every non-expired refresh token of all users and runs bcrypt against each until one matches. Cost grows linearly with the number of active tokens (a lookup key or token-id prefix would avoid this). Rows are never cleaned up.
4. **Event delivery is best effort** (publish after commit, no outbox, no confirms, no reconnect). A broker outage turns register and login into `500` after the data is already written.
5. **Event payloads use PascalCase** property names (default `System.Text.Json`), while HTTP success responses are camelCase and `ErrorResponse` is PascalCase.
6. **Duplicated or unused code:** `ExceptionMiddleware` is registered twice; `IRefreshTokenService.GenerateWithRaw` and `Validate` are unused; `GoogleAuth:ClientSecret`, the InMemory EF provider, `Microsoft.AspNetCore.OpenApi`, `HealthChecks.UI.Client` and `Npgsql.OpenTelemetry` are referenced or configured but unused.
7. **Compose configuration mismatch:** `appsettings.Development.json` points to PostgreSQL port `5433` (the host-published port) while the compose-run container needs `5432`; RabbitMQ credentials are hard-coded to `guest`/`guest` in appsettings and are not taken from compose variables.
8. **Security hygiene:** the signing key, DB password and RabbitMQ credentials in `appsettings*.json` are development defaults committed to source control and must be overridden outside local development. Access tokens cannot be revoked before expiry (no `jti`, no denylist), there is no logout endpoint, and the service has no brute-force protection. Logs contain user emails.
9. **No input validation** (email format, password length, null or empty fields); a body missing required fields can surface as `500`.
10. **Migration caveat:** automatic migration runs only in `Development`; other environments need manual migration. `AddNormalizedEmail` backfills `NormalizedEmail` with `''`, which breaks the unique index when more than one legacy row exists.
11. **Dockerfile** copies the unit- and integration-test project files for restore (needed because `dotnet restore` runs on the solution) and exposes only `8080`.
