# 3D Print Storefront

Next.js 16 web application for browsing, purchasing, and managing 3D-printed products.

## Quick Start

```bash
# Install dependencies
npm install

# Copy environment variables
cp .env.local.example .env.local
# Edit .env.local with your values

# Start dev server
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

## Environment Variables

| Variable | Description |
|----------|-------------|
| `NEXT_PUBLIC_API_GATEWAY_URL` | API gateway URL (default: `http://localhost:8080`) |
| `AUTH_SECRET` | Auth.js session encryption secret |
| `AUTH_URL` | Application URL (default: `http://localhost:3000`) |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | Stripe publishable key |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID |
| `GOOGLE_CLIENT_SECRET` | Google OAuth client secret |

## Tech Stack

- **Framework**: Next.js 16 (App Router, Turbopack)
- **UI**: Tailwind CSS v4 + shadcn/ui + Lucide icons
- **State**: TanStack Query v5
- **Forms**: Zod + React Hook Form
- **Auth**: Auth.js (next-auth)
- **3D**: @react-three/fiber + @react-three/drei
- **PDF**: pdfjs-dist
- **Payments**: Stripe.js

## Project Structure

- `src/app/` — Next.js App Router pages
- `src/components/` — React components
- `src/lib/` — Utilities, API client, auth config
- `src/hooks/` — Custom hooks and TanStack Query wrappers
- `src/providers/` — React context providers
- `src/schemas/` — Zod validation schemas
- `src/types/` — TypeScript type definitions
