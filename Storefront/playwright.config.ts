import { defineConfig } from "@playwright/test";

/**
 * Responsive, access-control, admin-flow and analytics checks.
 * Viewports follow the spec: phone (320), tablet (768), desktop (1280).
 * Run against a running stack (see specs/001-admin-dashboard-rbac/quickstart.md):
 *   PLAYWRIGHT_BASE_URL=http://localhost:3000 npx playwright test
 */
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3000";

const viewports = [
  { name: "phone-320", width: 320, height: 640 },
  { name: "tablet-768", width: 768, height: 1024 },
  { name: "desktop-1280", width: 1280, height: 800 },
] as const;

export default defineConfig({
  testDir: "./tests",
  testMatch: [
    "responsive/**/*.spec.ts",
    "access/**/*.spec.ts",
    "admin/**/*.spec.ts",
    "analytics/**/*.spec.ts",
  ],
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL,
    trace: "retain-on-failure",
  },
  projects: viewports.map(({ name, width, height }) => ({
    name,
    use: { viewport: { width, height } },
  })),
});
