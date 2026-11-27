# Storefront

Next.js (App Router) frontend for the 3D-printing e-commerce platform. It is a browser-facing client for the backend microservices and talks to them exclusively through the **API gateway** (`NEXT_PUBLIC_API_GATEWAY_URL`, default `http://localhost:8080`). It contains no database and no business logic of its own, apart from a thin server-side layer for authentication (Auth.js) and one PDF proxy route handler.

> Status: partially implemented. The browsing, search, chat, cart, order-creation, account and admin screens are wired to the backend. The Stripe card-payment step of checkout is **not** wired into the UI yet (see [Checkout and payment flow](#7-checkout-and-payment-flow)). Details of what is implemented versus placeholder are in [Implementation status](#10-implementation-status-and-known-gaps).

## Contents

1. [Role in the system](#1-role-in-the-system)
2. [Tech stack](#2-tech-stack)
3. [Project layout and routes](#3-project-layout-and-routes)
4. [Backend API usage](#4-backend-api-usage)
5. [Authentication and session flow](#5-authentication-and-session-flow)
6. [Client-side state and storage](#6-client-side-state-and-storage)
7. [Checkout and payment flow](#7-checkout-and-payment-flow)
8. [Configuration](#8-configuration)
9. [Running locally, building, linting](#9-running-locally-building-linting)
10. [Implementation status and known gaps](#10-implementation-status-and-known-gaps)

---

## 1. Role in the system

```
Browser ──► Storefront (Next.js, :3000)
              │  ├─ /api/auth/*                       (Auth.js, server side)
              │  └─ /api/documents/{id}/download      (PDF proxy route handler)
              │
              └──► API gateway (:8080 by default) ──► Identity / Catalog / Inventory / Orders /
                                                      Payments / Documents / Notifications /
                                                      GraphRAG (search, chat, recommendations)

Browser ──► MinIO (:9000 by default)  – product images / 3D models, loaded directly by the browser
```

- Almost all API calls are made **from the browser** with `fetch` (see `src/lib/api/client.ts`). Only authentication calls (`/auth/*`) and the document download handler run on the Next.js server.
- The frontend never talks to individual services; every path below is relative to the gateway base URL.
- Product images are served by object storage (MinIO). URLs returned by the backend are rewritten to the public MinIO origin by `resolveMediaUrl()` (see [Configuration](#8-configuration)).

Gateway path prefixes used by the code:

| Gateway prefix | Backend capability (as inferred from the paths) |
|---|---|
| `/auth/*` | Identity: register, login, Google login, refresh, profile |
| `/products/*` | Product catalog (CRUD, images, attributes, relations) |
| `/inventory/*` | Stock levels and adjustments |
| `/orders/*` | Orders |
| `/payments/*` | Payments and refunds |
| `/documents/*` | Generated invoices/receipts metadata |
| `/notifications/*` | Notification history |
| `/graphrag/api/*` | GraphRAG service: semantic search, chat, recommendations |

---

## 2. Tech stack

Versions are taken from `package.json`.

| Area | Technology |
|---|---|
| Framework | Next.js `16.2.9` (App Router, Turbopack config present, `proxy.ts` instead of `middleware.ts`) |
| UI runtime | React `19.2.4`, TypeScript `^5` |
| Styling | Tailwind CSS `^4` (`@tailwindcss/postcss`), `tw-animate-css`, `class-variance-authority`, `clsx`, `tailwind-merge` |
| Component library | shadcn/ui (`components.json`: style `base-nova`, base colour `neutral`) built on `@base-ui/react ^1.6`; `lucide-react` icons; `cmdk`; `sonner` toasts; `next-themes` (light/dark/system) |
| Server state / data fetching | `@tanstack/react-query ^5` (default `staleTime` 60 s, `gcTime` 5 min, `retry: 1`, no refetch on window focus) over a small `fetch` wrapper |
| Client state | React Context (`CartProvider`) persisted to `localStorage`; Auth.js session via `SessionProvider`; no Redux/Zustand |
| Forms and validation | `react-hook-form ^7`, `@hookform/resolvers`, `zod ^4` (schemas in `src/schemas`) |
| Authentication | `next-auth ^5.0.0-beta.31` (Auth.js v5): Credentials provider + Google provider, JWT session strategy |
| Payments | `@stripe/stripe-js ^9.8`, `@stripe/react-stripe-js ^6.6` (a `PaymentForm` component exists but is not mounted; see below) |
| 3D preview | `three ^0.184`, `@react-three/fiber ^9`, `@react-three/drei ^10` (GLTF viewer) |
| PDF preview | `pdfjs-dist ^6` (worker loaded from cdnjs) |
| Linting | ESLint 9 with `eslint-config-next` (core-web-vitals + typescript) |
| Testing | `@playwright/test` is listed as a dev dependency, but there is no Playwright config or test file in the project |

`next.config.ts`:

- `images.remotePatterns` allows `http://localhost:9000` and `https://*.minio.*` for `next/image`.
- `transpilePackages: ["pdfjs-dist"]` and `canvas` aliased to an empty module (Turbopack and webpack) so `pdfjs-dist` builds in the browser.

Path alias: `@/*` maps to `./src/*`.

---

## 3. Project layout and routes

```
Storefront/
├── proxy.ts                     # Route protection (auth + staff role check)
├── next.config.ts
├── components.json              # shadcn/ui config
├── src/
│   ├── app/
│   │   ├── layout.tsx           # Providers: Session, React Query, Theme, Cart, Tooltip, Toaster
│   │   ├── error.tsx, loading.tsx, not-found.tsx
│   │   ├── (shop)/              # Public storefront (header, footer, chat widget)
│   │   ├── (auth)/              # /login, /register (centered card layout)
│   │   ├── (account)/           # Signed-in customer area (sidebar nav)
│   │   ├── (staff)/admin/       # Staff/admin area (sidebar nav)
│   │   └── api/
│   │       ├── auth/[...nextauth]/route.ts
│   │       └── documents/[documentId]/download/route.ts
│   ├── components/
│   │   ├── ui/                  # shadcn/ui primitives
│   │   ├── layout/              # SiteHeader, SiteFooter
│   │   ├── products/            # ProductCard, ProductGallery, ProductViewer (3D), AvailabilityBadge, ProductRecommendations
│   │   ├── search/              # SearchFilterPanel, SearchBar
│   │   ├── cart/                # CartDrawer
│   │   ├── checkout/            # AddressForm, PaymentForm, OrderConfirmation
│   │   ├── orders/              # OrderCard, PaymentStatus
│   │   ├── documents/           # PdfViewer
│   │   ├── chat/                # ChatWidget
│   │   ├── admin/               # ProductForm
│   │   └── shared/              # EmptyState, ErrorFallback, loading skeletons
│   ├── hooks/
│   │   ├── use-auth.ts          # Session helper (role, isStaff, accessToken)
│   │   ├── use-cart.ts          # Cart context accessor
│   │   └── queries/             # React Query hooks per API area
│   ├── lib/
│   │   ├── api/                 # API clients (client.ts + one file per service)
│   │   ├── auth.ts, auth.config.ts  # Auth.js configuration
│   │   ├── constants.ts         # Route helpers and React Query keys
│   │   └── utils.ts             # cn, formatCurrency (default EUR), formatDate, resolveMediaUrl, correlation ID
│   ├── providers/               # Query, Theme, Cart providers
│   ├── schemas/                 # Zod schemas (auth, checkout, product, inventory)
│   └── types/                   # API payload types
```

Route groups (`(shop)`, `(auth)`, `(account)`, `(staff)`) do not appear in URLs. The `(account)` and `(staff)` layouts only render navigation; **access control is enforced by `proxy.ts`**, not by the layouts.

### Route table

"Access" is what `proxy.ts` enforces. Public paths are `/`, `/products*`, `/search*`, `/login*`, `/register*`, `/api/auth*`; everything else requires a session, and `/admin*` additionally requires role `Admin`, `Manager` or `Support`. Unauthenticated users are redirected to `/login?callbackUrl=<path>`; signed-in users without a staff role are redirected to `/`.

| URL | Group | Purpose | Access |
|---|---|---|---|
| `/` | shop | Landing page with hero and 8 featured products (`GET /products?page=1&pageSize=8`) | Public |
| `/products` | shop | Catalog with text search, material and printer filters, pagination (12 per page) | Public |
| `/products/[id]` | shop | Product detail: gallery, stock badge, add to cart, print settings, dimensions, tags, attributes, optional 3D viewer, recommendations | Public |
| `/search` | shop | Natural-language search against GraphRAG with material/printer/price/rating filters | Public |
| `/cart` | shop | Cart contents, quantity edit, totals | Login required (not in public list) |
| `/checkout` | shop | Two-step checkout: shipping address, review and place order | Login required (page also shows a sign-in prompt if no session) |
| `/login` | auth | Email/password and Google sign-in | Public |
| `/register` | auth | Create account, then auto sign-in | Public |
| `/orders` | account | "My Orders" list with status filter | Login required |
| `/orders/[id]` | account | Order detail, timeline, shipping address, items, payment status, cancel (when `PendingPayment` or `Paid`) | Login required |
| `/documents` | account | Look up invoices/receipts by order ID and preview PDF | Login required |
| `/notifications` | account | Notification history table with status filter and detail dialog | Login required |
| `/profile` | account | Read-only email and role from the session | Login required |
| `/admin/products` | staff | Product list with status filter | Staff role |
| `/admin/products/new` | staff | Create product | Staff role |
| `/admin/products/[id]` | staff | Edit or delete product | Staff role |
| `/admin/inventory` | staff | Stock table, "depleted only" filter, stock adjustment dialog | Staff role |
| `/admin/orders` | staff | All orders with status and user ID filters | Staff role |
| `/admin/orders/[id]` | staff | Read-only order detail with payment status | Staff role |
| `/admin/payments` | staff | Payments list with status filter; refund dialog for `Completed` payments | Staff role |
| `/api/auth/[...nextauth]` | api | Auth.js handlers (`GET`, `POST`) | Public |
| `/api/documents/[documentId]/download` | api | Server-side PDF proxy (see below) | Session required (checked in the handler) |

Other notes:

- The same access rules are duplicated in the `authorized` callback in `src/lib/auth.config.ts`.
- The proxy matcher skips `_next/static`, `_next/image`, `favicon.ico` and any path containing a dot.
- The "Admin" link in the header is shown only to staff roles, and only in the desktop navigation (the mobile menu has no admin link).
- Staff roles are not differentiated further: `Admin`, `Manager` and `Support` all see the same admin pages. `useAuth()` exposes `isAdmin`, but no page uses it to restrict features.

---

## 4. Backend API usage

All requests go to `${NEXT_PUBLIC_API_GATEWAY_URL}${path}`. The shared client in `src/lib/api/client.ts` behaves as follows:

- Adds `X-Correlation-Id: <crypto.randomUUID()>` to every request.
- Adds `Authorization: Bearer <accessToken>` unless the call uses `skipAuth`. In the browser the token comes from `getSession()` (`next-auth/react`).
- Sends JSON bodies (`Content-Type: application/json`), except `apiPostFormData` for uploads.
- On a non-2xx response throws `ApiClientError(status, code, details)` using the body shape `{ "error": string, "code": string, "details"?: object }`; on `401` in the browser it redirects to `/login`. `204` returns `undefined`.
- Query strings are built by `buildQueryString` (skips `undefined`, `null`, `''`; repeats array keys).
- `serverApiGet` (server-side, `cache: 'no-store'`) exists but no page currently uses it.

Paginated list endpoints are expected to return:

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0 }
```

### 4.1 Calls made by the UI

"Auth" is whether the Storefront sends the bearer token. "UI use" says where it is called from; "defined, unused" means the client function exists but no screen calls it.

| Method | Gateway path | Purpose | Auth | UI use |
|---|---|---|---|---|
| POST | `/auth/register` | Create account | No | `/register` |
| POST | `/auth/login` | Email/password login | No | Auth.js Credentials `authorize` (server) |
| POST | `/auth/google` | Exchange Google `id_token` for platform tokens | No | Auth.js `jwt` callback (server) |
| POST | `/auth/refresh` | Refresh access token | No | Auth.js `jwt` callback (server) |
| GET | `/auth/me` | Fetch profile | Bearer | Defined, unused (profile page reads the session) |
| GET | `/products` | List/filter products | No | Home, `/products`, `/admin/products` |
| GET | `/products/{id}` | Product detail (route param may be an ID; hook names it `idOrSlug`) | No | `/products/[id]`, `/admin/products/[id]` |
| POST | `/products` | Create product | Bearer | `/admin/products/new` |
| PUT | `/products/{id}` | Update product | Bearer | `/admin/products/[id]` |
| DELETE | `/products/{id}` | Delete product | Bearer | `/admin/products/[id]` |
| POST | `/products/{id}/images` (multipart `file`) | Upload image | Bearer | Defined, unused |
| DELETE | `/products/{id}/images/{imageId}` | Delete image | Bearer | Defined, unused |
| POST | `/products/{id}/attributes` | Add attribute `{key, value}` | Bearer | Defined, unused |
| DELETE | `/products/{id}/attributes/{attributeId}` | Remove attribute | Bearer | Defined, unused |
| POST | `/products/{id}/relations` | Add relation `{relatedProductId, type}` | Bearer | Defined, unused |
| DELETE | `/products/{id}/relations/{relationId}` | Remove relation | Bearer | Defined, unused |
| GET | `/inventory/{productId}` | Stock for one product | No | Product detail page (availability) |
| GET | `/inventory?depletedOnly&page&pageSize` | Stock list | Bearer | `/admin/inventory` |
| POST | `/inventory/{productId}/adjustments` | Adjust stock `{delta, reason}` | Bearer | `/admin/inventory` |
| POST | `/orders` | Create order | Bearer | `/checkout` |
| GET | `/orders?userId&status&fromDate&toDate&page&pageSize` | List orders | Bearer | `/orders`, `/admin/orders` |
| GET | `/orders/{id}` | Order detail | Bearer | `/orders/[id]`, `/admin/orders/[id]` |
| GET | `/orders/user/{userId}` | Orders for a user | Bearer | Defined, unused |
| POST | `/orders/{id}/cancel` | Cancel `{reason}` | Bearer | `/orders/[id]` (reason is hard-coded "Customer requested cancellation") |
| PUT | `/orders/{id}/shipping-address` | Update address | Bearer | Hook defined and imported by order page, no UI calls it |
| POST | `/payments` | Create payment | Bearer | Hook defined, unused |
| POST | `/payments/{id}/process` | Process payment | Bearer | Hook defined, unused |
| POST | `/payments/{id}/refund` | Refund `{amount?}` (`{}` for full refund) | Bearer | `/admin/payments` |
| GET | `/payments/{id}` | Payment detail | Bearer | Hook defined, unused |
| GET | `/payments/by-order/{orderId}` | Payment for an order | Bearer | `/orders/[id]`, `/admin/orders/[id]` |
| GET | `/payments?userId&orderId&status&method&fromDate&toDate&page&pageSize` | List payments | Bearer | `/admin/payments` |
| GET | `/documents?orderId&paymentId&documentType&status&page&pageSize` | List document metadata | Bearer | `/documents` (query only runs when `orderId` or `paymentId` is set) |
| GET | `/documents/{id}` | Document metadata | Bearer | Download route handler (server) |
| GET | `/notifications?eventId&eventType&recipient&status&page&pageSize` | List notifications | Bearer | `/notifications` |
| GET | `/notifications/{id}` | Notification detail | Bearer | Hook defined, unused |
| POST | `/graphrag/api/search` | Semantic search | No | `/search` |
| POST | `/graphrag/api/chat` | RAG chat | No | `ChatWidget` |
| POST | `/graphrag/api/recommend` | Recommendations for a product | No | `ProductRecommendations` on product detail |

The Storefront does not read the user ID from the token for orders; `/orders` sends no `userId` filter for customers, so the gateway/Orders service is expected to scope results to the caller. Admin order and payment lists are the same endpoints used without scoping.

Besides the gateway, the server-side download handler also fetches directly from MinIO (see [section 7](#7-checkout-and-payment-flow)).

### 4.2 Types and payload shapes

Types live in `src/types/*.ts`. Note that the frontend types are hand-written; there is no code generation from backend contracts.

**Auth** (`types/user.ts`)

`POST /auth/register`, `/auth/login`, `/auth/google` responses (`AuthResponse`):

```json
{
  "user": {
    "id": "uuid",
    "email": "user@example.com",
    "role": "Customer",
    "provider": "Internal",
    "createdAt": "2025-01-01T12:00:00Z"
  },
  "tokens": { "accessToken": "jwt", "refreshToken": "opaque-or-jwt", "expiresIn": 900 }
}
```

- `role`: `Admin | Customer | Manager | Support`; `provider`: `Internal | Google`; `expiresIn` is in seconds.
- Requests: register/login send `{ "email", "password" }`; Google sends `{ "idToken" }`; refresh sends `{ "refreshToken" }` and expects `{ accessToken, refreshToken, expiresIn }` (no `user`).
- Error shape on auth failures is read as `{ "error": "message" }`.

**Product** (`types/product.ts`)

```json
{
  "id": "uuid",
  "name": "Articulated Dragon",
  "slug": "articulated-dragon",
  "shortDescription": "...",
  "longDescription": "...",
  "price": 24.9,
  "currency": "EUR",
  "status": "Active",
  "categoryId": "uuid",
  "settings": {
    "layerHeight": 0.2, "infillPercentage": 20, "nozzleSize": 0.4,
    "printTimeMinutes": 360, "filamentUsedGrams": 80, "supportsRequired": false
  },
  "size": { "widthMm": 120, "heightMm": 40, "depthMm": 30 },
  "tags": ["dragon"],
  "attributes": [{ "attributeId": "uuid", "key": "color", "value": "red" }],
  "relatedProducts": [{ "relationId": "uuid", "relatedProductId": "uuid", "type": "Accessory" }],
  "supportedMaterials": ["PLA", "PETG"],
  "compatiblePrinters": ["FDM"],
  "images": [{ "imageId": "uuid", "objectKey": "...", "url": "http://minio:9000/...", "isPrimary": true }],
  "downloadCount": 0,
  "averageRating": 4.5,
  "createdAt": "...",
  "updatedAt": null
}
```

- `status`: `Draft | Active | Inactive | OutOfStock | Discontinued`; `Material`: `PLA | ABS | PETG | Resin | Nylon | TPU`; `PrinterType`: `FDM | SLA | SLS`.
- List items (`ProductListItem`) contain a subset (no `longDescription`, `categoryId`, `settings`, `size`, `tags`, `attributes`, `relatedProducts`).
- Product list filters: `categoryId, minPrice, maxPrice, tags[], material, printerType, search, status, page, pageSize`. The UI sets only `search`, `material`, `printerType`, `page`, `pageSize` (and `status` in admin).
- Create/update send the product form values (`schemas/product.ts`): `name, shortDescription, longDescription, price, categoryId, currency, settings, size, tags, supportedMaterials, compatiblePrinters`. Update omits `categoryId` in the schema type, but the edit page still sends the whole form object.
- The product page also reads an optional `model3dUrl` (accessed via `as any`; it is not in the `Product` type) and shows the GLTF viewer when present.
- Mutations for images/attributes/relations return `{ imageId, url }`, `{ attributeId }`, `{ relationId }` respectively.

**Inventory** (`lib/api/inventory.ts`)

API shape, mapped by the client to `{ productId, onHand, reserved, available }`:

```json
{ "productId": "uuid", "onHandQuantity": 100, "reservedQuantity": 5, "availableQuantity": 95 }
```

Adjustment request: `{ "delta": -3, "reason": "Damaged" }` (`delta` must be non-zero, `reason` required). The availability badge shows In Stock, Low Stock (1 to 10) or Out of Stock (0 or less).

**Orders** (`types/order.ts`)

`POST /orders` request (`CreateOrderRequest`):

```json
{
  "userId": "uuid",
  "currency": "EUR",
  "notes": null,
  "recipientEmail": "user@example.com",
  "shippingAddress": {
    "street": "1 Main St", "city": "Cluj", "state": "CJ", "country": "RO",
    "zipCode": "400000", "fullName": "Jane Doe", "phone": null
  },
  "items": [
    {
      "productId": "uuid", "productName": "Articulated Dragon", "unitPrice": 24.9,
      "quantity": 2, "currency": "EUR", "productImageUrl": "http://..."
    }
  ]
}
```

Response: `{ "orderId": "uuid" }`. Product name, price and image are sent from the client cart snapshot.

Order (`GET /orders/{id}`): `id, userId, status, totalAmount, currency, notes, shippingAddress, items[{ id, productId, productName, unitPrice, quantity, currency, productImageUrl, lineTotal }], createdAt, updatedAt, paidAt, shippedAt, deliveredAt, cancelledAt, cancellationReason`. `status`: `PendingPayment | Paid | Processing | Shipped | Delivered | Cancelled | PaymentFailed`.

**Payments** (`types/payment.ts`)

Create request (`CreatePaymentRequest`):

```json
{
  "orderId": "uuid", "amount": 49.8, "currency": "EUR",
  "method": "CreditCard", "paymentToken": "pm_...", "idempotencyKey": "uuid"
}
```

Payment: `id, orderId, userId, amount, currency, status, method, idempotencyKey, refundedAmount, expiresAt, transactions[{ id, type, amount, currency, providerTransactionId, status, createdAt }], createdAt, updatedAt`. `status`: `Pending | Processing | Completed | Failed | Cancelled | PartiallyRefunded | Refunded`; `method`: `CreditCard | DebitCard`; transaction `type`: `Charge | Refund`.

**Documents** (`types/document.ts`)

```json
{
  "id": "uuid", "documentType": "Invoice", "idempotencyKey": "...", "templateVersion": "v1",
  "status": "Completed", "storageObjectKey": "invoices/....pdf", "storageBucket": "documents",
  "orderId": "uuid", "paymentId": "uuid", "userId": "uuid", "correlationId": null,
  "failureReason": null, "fileSizeBytes": 12345,
  "createdAt": "...", "completedAt": "...", "failedAt": null
}
```

`documentType`: `Invoice | Receipt | CreditNote`; `status`: `Pending | Completed | Failed`.

**Notifications** (`types/notification.ts`)

```json
{
  "id": "uuid", "idempotencyKey": "...", "eventType": "OrderCreated",
  "notificationType": "OrderConfirmation", "channel": "Email", "recipient": "user@example.com",
  "subject": "...", "bodyPreview": "...", "status": "Sent", "attemptCount": 1,
  "correlationId": null, "errorDetails": null,
  "createdAt": "...", "sentAt": "...", "updatedAt": "..."
}
```

`notificationType`: `Welcome | OrderConfirmation | PaymentReceipt | ShippingUpdate | OrderCancelled | RefundProcessed`; `channel`: `Email | Sms | Push`; `status`: `Pending | Sent | Failed | DeadLettered`.

**GraphRAG** (`types/search.ts`; note snake_case fields)

Search request/response:

```json
{
  "query": "durable bracket for outdoor use",
  "filters": { "materials": ["PETG"], "printers": ["FDM"], "price_min": 5, "price_max": 50, "min_rating": 4 },
  "limit": 10
}
```

```json
{
  "results": [
    {
      "product_id": "uuid", "name": "...", "slug": "...", "score": 0.82,
      "vector_score": 0.8, "graph_score": 0.6, "rerank_score": 0.9, "metadata": {}
    }
  ],
  "total_candidates": 40,
  "intent": "product_search",
  "answer": null
}
```

Chat: request `{ "query": "...", "filters"?: {...} }`, response `{ "answer": "...", "sources": [ProductResult], "intent": "..." }`. Recommend: request `{ "product_id": "uuid", "limit": 8 }`, response is a `SearchResponse`. `SearchFilters` also allows `category_id`, `status`, `tags`.

---

## 5. Authentication and session flow

Implemented with Auth.js v5 (`src/lib/auth.ts`, route at `/api/auth/[...nextauth]`), JWT session strategy, custom sign-in page `/login`.

### Login and registration

1. **Email/password login** (`/login`): `signIn('credentials', { email, password, redirect: false })`. The Credentials `authorize` function (server side) calls `POST /auth/login` on the gateway. On success it builds the Auth.js user `{ id, email, role, accessToken, refreshToken, accessTokenExpires = now + expiresIn*1000 }`; on any failure it returns `null` (the UI shows "Invalid email or password"). The login form requires a valid email and a password of at least 6 characters.
2. **Registration** (`/register`): the browser calls `POST /auth/register` directly, then signs in using the credentials provider and does a full page redirect to `/`. Password must be at least 8 characters and match the confirmation. If the auto-login fails, the user is asked to sign in manually.
3. **Google login** (both pages): `signIn('google', { callbackUrl: '/' })`. In the `jwt` callback, when `account.provider === 'google'` and an `id_token` is present, the server calls `POST /auth/google { idToken }` and stores the returned platform user ID, role and tokens in the Auth.js token. If that call fails the callback returns the token as is (no platform tokens, so API calls will be unauthenticated).

### Token storage and forwarding

- Tokens live inside the **Auth.js encrypted JWT session cookie** (httpOnly). There is no separate token storage in `localStorage`.
- The `session` callback exposes `session.user.id`, `session.user.role`, `session.accessToken` and `session.error` to the client. The refresh token is **not** exposed in the session.
- For API calls the browser client calls `getSession()` (which hits `/api/auth/session`) on each authenticated request and sends `Authorization: Bearer <accessToken>` to the gateway. For the PDF download the route handler reads the session on the server via `auth()`.
- `SessionProvider` wraps the app, so components use `useSession()`/`useAuth()` for the user, role and `isStaff` (`Admin | Manager | Support`).

### Refresh

The `jwt` callback checks `Date.now() < token.accessTokenExpires`; when the token has expired it calls `POST /auth/refresh { refreshToken }` and stores the new access token, refresh token and expiry. If refresh fails, it sets `token.error = 'RefreshAccessTokenError'` (exposed as `session.error`). Nothing in the UI reacts to `session.error`; a stale token is eventually rejected by the gateway with 401, and the client redirects to `/login` (the session cookie is not cleared by that redirect).

### Role-based access

- The role comes from the backend login response and is stored in the JWT at login; it is not re-fetched.
- `proxy.ts` blocks all non-public paths without a session and blocks `/admin*` for users whose `role` is not `Admin`, `Manager` or `Support`.
- The backend is expected to enforce authorization independently; the Storefront only gates UI routes.
- Sign out: header menu calls `signOut({ callbackUrl: '/' })`.

---

## 6. Client-side state and storage

| What | Where | Form |
|---|---|---|
| Cart | `localStorage`, key `storefront-cart` | `{ "items": CartItem[], "updatedAt": "ISO timestamp" }` |
| Auth session and tokens | Auth.js session cookie | Encrypted JWT (see section 5) |
| Theme | `next-themes` (its default storage, `localStorage` key `theme`), default `system` | `light` / `dark` / `system` |
| Server data cache | React Query in-memory cache | Per-key cache, not persisted |
| Chat history | React state inside `ChatWidget` | In-memory only, lost on reload |
| Checkout form/step | React state in the checkout page | In-memory only |

`CartItem` (provided by `CartProvider`, `src/providers/cart-provider.tsx`):

```json
{
  "productId": "uuid",
  "productName": "Articulated Dragon",
  "slug": "articulated-dragon",
  "price": 24.9,
  "currency": "EUR",
  "quantity": 2,
  "imageUrl": "http://localhost:9000/...",
  "material": "PLA"
}
```

Behaviour:

- The cart is hydrated from `localStorage` after mount and written back on each change. It is per-browser and not tied to the user, and it is not synchronised with any backend.
- `addItem` merges by `productId` (quantities add up); `updateQuantity` ignores values below 1; the cart total uses the first item's currency (default `EUR`).
- The price is a snapshot from when the item was added; prices are not re-validated against the catalog at checkout.
- The product page adds the first entry of `supportedMaterials` as the cart item's `material`; there is no material selector.
- No cookies are set by the application code other than those managed by Auth.js and `next-themes`.

---

## 7. Checkout and payment flow

### What the code does today

1. `/cart` shows the cart; "Proceed to Checkout" goes to `/checkout`.
2. **Shipping step**: `AddressForm` (validated by `shippingAddressSchema`: street, city, state, country and ZIP are required; full name and phone optional).
3. **Review step**: order summary and address, then "Place Order".
4. The page calls `POST /orders` with the user ID from the session, the cart items, shipping address and the user's email as `recipientEmail` (see payload above).
5. On success it stores the returned `orderId`, **clears the cart** and shows `OrderConfirmation` ("Order Placed!") with links to `/orders/{orderId}` and the catalog.

The order starts in the backend's initial state (the frontend expects `PendingPayment`). On `/orders/[id]` the page loads `GET /payments/by-order/{id}` (no retries) and renders the payment status badge and amount if a payment exists. The order can be cancelled while `PendingPayment` or `Paid`.

### Stripe payment (intended, not connected)

The building blocks for card payment exist but are **not connected to the checkout page**:

- `src/components/checkout/payment-form.tsx` renders Stripe's `CardElement`, calls `stripe.createPaymentMethod({ type: 'card', card })` and hands the `paymentMethod.id` to an `onPaymentMethod` callback.
- `createPayment` (`POST /payments`, with `paymentToken`, `idempotencyKey`, `method`) and `processPayment` (`POST /payments/{id}/process`) exist as API functions and React Query mutation hooks (`useCreatePayment`, `useProcessPayment`).
- `PaymentStatus` has an optional "Retry Payment" button for `Failed` payments, but no caller passes `onRetry`.

However, no component renders `<Elements>`, `loadStripe` is never called, `PaymentForm` is not imported anywhere, and `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` is not read by any code. As a result, the flow currently stops after order creation; the intended sequence (create order, create payment with the Stripe payment-method ID, process payment, refresh order/payment) is not executed by the UI. The only payment action that is wired in the UI is the staff **refund** (`POST /payments/{id}/refund`).

### Documents

- `/documents` takes an order ID and calls `GET /documents?orderId=...`; only documents with status `Completed` get a "View" button.
- The PDF viewer (`pdfjs-dist`, canvas rendering, page navigation and zoom 50-300%) loads `/api/documents/{id}/download` from the Storefront itself.
- The route handler (`src/app/api/documents/[documentId]/download/route.ts`):
  1. Requires a session (else `401`).
  2. Fetches `GET {gateway}/documents/{id}` with the bearer token (propagates the failing status as "Document not found").
  3. Requires `storageObjectKey` and `storageBucket` (else `404 Document not ready`).
  4. Fetches the file **directly from MinIO** at `http://localhost:9000/{bucket}/{objectKey}` (this host is hard-coded; it is not configurable and will not work outside a setup where MinIO is on the Next.js server's localhost:9000), unauthenticated.
  5. Returns the bytes as `application/pdf` with `Content-Disposition: inline; filename="{documentId}.pdf"`. Errors map to `502` (MinIO failed) or `500`.
- The pdf.js worker script is loaded from `cdnjs.cloudflare.com`, so the document preview requires internet access from the browser.

### Chat widget and GraphRAG

- `ChatWidget` is mounted only in the `(shop)` layout (floating "Shopping Assistant" button on public shop pages, including the cart and checkout pages, but not on account, admin or auth pages).
- Each message calls `POST /graphrag/api/chat` with `{ query }` only (no history and no filters are sent) and renders `answer`, plus up to 3 `sources` as links to `/products/{product_id}`. On error it shows a generic apology message.
- `/search` calls `POST /graphrag/api/search` (`limit` 10) when a query is submitted, with the filters from `SearchFilterPanel`, and lists results with their `score`, `total_candidates` and `intent`. The `answer` field is not displayed.
- The product page's "You Might Also Like" row calls `POST /graphrag/api/recommend`; cards are built from the search result only (name and slug), so price, image and description are placeholders (price `0`, no image) and the carousel is effectively a list of names.

---

## 8. Configuration

Environment variables are read from `.env.local` (git-ignored via `.env*`; there is no `.env.example` committed).

| Variable | Used by | Default | Notes |
|---|---|---|---|
| `NEXT_PUBLIC_API_GATEWAY_URL` | `lib/api/client.ts`, `lib/api/auth.ts`, download route handler | `http://localhost:8080` | API gateway base URL (browser and server) |
| `NEXT_PUBLIC_MINIO_PUBLIC_URL` | `lib/utils.ts` `resolveMediaUrl` | `http://localhost:9000` | Public MinIO origin used to rewrite image URLs. Any URL starting with the host `minio` (with optional port) is rewritten to this origin; URLs without a scheme get `http://` prepended |
| `GOOGLE_CLIENT_ID` | `lib/auth.ts` | none | Google OAuth client ID (server only) |
| `GOOGLE_CLIENT_SECRET` | `lib/auth.ts` | none | Google OAuth client secret (server only) |
| `AUTH_SECRET` | Auth.js (implicit) | none | Required in production to sign/encrypt the session JWT; not referenced in code directly |
| `AUTH_URL` | Auth.js (implicit) | inferred (`http://localhost:3000` in dev) | Public URL of the app, needed when behind a proxy or in production; not referenced in code directly |

Example `.env.local` (placeholders only):

```dotenv
NEXT_PUBLIC_API_GATEWAY_URL=http://localhost:8080
NEXT_PUBLIC_MINIO_PUBLIC_URL=http://localhost:9000
AUTH_SECRET=<generate with: npx auth secret   or   openssl rand -base64 32>
AUTH_URL=http://localhost:3000
GOOGLE_CLIENT_ID=<google-oauth-client-id>
GOOGLE_CLIENT_SECRET=<google-oauth-client-secret>
```

`NEXT_PUBLIC_*` values are inlined into the browser bundle at build time. Google sign-in requires an OAuth client whose authorized redirect URI is `<AUTH_URL>/api/auth/callback/google`.

No Stripe variable is used by the code at the moment (see section 7).

---

## 9. Running locally, building, linting

### Prerequisites

- Node.js compatible with Next.js 16 (Node 20 or later is recommended; `@types/node` is `^20`) and npm (a `package-lock.json` is committed).
- For anything beyond the static shell, the backend must be reachable: the **API gateway on `http://localhost:8080`** with the downstream services (Identity, Catalog, Inventory, Orders, Payments, Documents, Notifications, GraphRAG) and **MinIO on `http://localhost:9000`** (product images and generated PDFs). Start the backend stack with the repository's Docker Compose setup (see the root README); this project does not ship its own compose file.

### Commands

```bash
npm install            # install dependencies
npm run dev            # next dev  -> http://localhost:3000
npm run build          # next build (production build)
npm run start          # next start (serve the production build)
npm run lint           # eslint (next core-web-vitals + typescript rules)
```

Create `.env.local` first (see section 8). With Google credentials omitted, only email/password login works. Without a running gateway, pages render but data calls fail and show error fallbacks.

There is no test script. `@playwright/test` is installed, but no tests or config exist.

---

## 10. Implementation status and known gaps

### Implemented (wired to the backend)

- Public catalog: home, product list (search, material, printer filters, pagination), product detail with stock badge, print settings, dimensions, tags, attributes, optional 3D viewer.
- GraphRAG search page with filters, floating chat assistant, product recommendations.
- Email/password and Google authentication with token refresh and role-aware route protection.
- Local cart, shipping address step, order creation, confirmation.
- Customer area: orders list and detail with cancellation, document lookup with in-browser PDF preview, notification history, read-only profile.
- Staff area: product create/edit/delete, inventory list with stock adjustments, all orders (list and read-only detail), payments list with refunds.
- Light/dark theme, responsive layouts, loading skeletons, empty and error states.

### Placeholder, partial or unused

- **Card payment is not wired into checkout.** `PaymentForm`, `createPayment`, `processPayment`, and the Stripe packages are present but unused; there is no `loadStripe`/`<Elements>`, and `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` is not read. Orders are created but cannot be paid from the UI.
- Product images, attributes and relations: API functions exist, but the admin product form has no image upload or attribute/relation management UI.
- Orders: `useUpdateShippingAddress` is imported by the order page but no edit-address UI exists. Admin order detail is read-only (no ship/deliver/status actions).
- `getMe`, `getOrdersByUserId`/`useUserOrders`, `usePayment`, `useNotification`, `serverApiGet` and the `*Server` helpers are unused. Components `SearchBar`, `CartDrawer` and `OrderCard` are not used by any page (the header cart button links to `/cart`; `SearchBar` pushes `/search?q=...`, which the search page does not read).
- `@playwright/test` is a dependency with no tests. `AUTH_SECRET`/`AUTH_URL` are required by Auth.js but not documented in any committed example file.
- `/api/auth` refresh failures are stored as `session.error` but nothing handles it.

### Inconsistencies and caveats noticed in the code

- The download route handler hard-codes MinIO at `http://localhost:9000`, while images use the configurable `NEXT_PUBLIC_MINIO_PUBLIC_URL`.
- Select dropdowns (orders, products, notifications, payments, admin lists) have an "All ..." item with value `all`; the handlers pass `'all'` through as the filter value (`status=all`, `material=all`, `printerType=all`), because only an empty string is mapped to `undefined`.
- `/cart` and `/checkout` are not in the public paths in `proxy.ts`, so guests are redirected to `/login` when they open the cart.
- Role-based gating is a single "staff" check; there is no per-role distinction among `Admin`, `Manager` and `Support`.
- `Product` has no `model3dUrl` in its type, but the product page reads it through a type cast.
- The profile page shows only the session email and role (no backend call; `/auth/me` is unused).
- The password minimum differs between login (6) and register (8) validation.
- `public/` contains only the default create-next-app SVGs; no app-specific static assets.
- The layouts for `(account)` and `(staff)` are client components and do not check authentication themselves; they depend entirely on `proxy.ts`.
