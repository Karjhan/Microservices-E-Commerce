import { expect, test, type Page } from "@playwright/test";

const GATEWAY_URL = process.env.NEXT_PUBLIC_API_GATEWAY_URL ?? "http://localhost:8080";
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? "admin@store.local";
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD;

const STAFF_NAV_ITEMS = ["Products", "Inventory", "Orders", "Payments", "Audit"];

async function signIn(page: Page, email: string, password: string) {
  await page.goto("/login");
  await page.fill("#email", email);
  await page.fill("#password", password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/login"));
}

test.describe("staff area guard", () => {
  test("signed-out visitors are sent to sign-in", async ({ page }) => {
    await page.goto("/admin/products");
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByText("Sign in to your account")).toBeVisible();
  });

  test("signed-out visitors cannot open the audit page either", async ({ page }) => {
    await page.goto("/admin/audit");
    await expect(page).toHaveURL(/\/login/);
  });

  test("a customer is redirected away from every admin page", async ({ page, request }) => {
    const email = `e2e-customer-${Date.now()}@example.com`;
    const password = "Customer-Pass-1234";
    const registered = await request.post(`${GATEWAY_URL}/auth/register`, {
      data: { email, password },
    });
    expect(registered.ok()).toBeTruthy();

    await signIn(page, email, password);

    for (const path of ["/admin/products", "/admin/inventory", "/admin/orders", "/admin/payments", "/admin/audit"]) {
      await page.goto(path);
      await expect(page, `${path} must not be reachable by a customer`).not.toHaveURL(/\/admin/);
    }
  });

  test.describe("administrator", () => {
    test.skip(!ADMIN_PASSWORD, "Set E2E_ADMIN_PASSWORD to the development admin password.");

    test("sees every staff navigation item", async ({ page }) => {
      await signIn(page, ADMIN_EMAIL, ADMIN_PASSWORD!);
      await page.goto("/admin/products");

      const nav = page.getByTestId("staff-nav");
      for (const label of STAFF_NAV_ITEMS) {
        await expect(nav.getByRole("link", { name: label, exact: true })).toBeVisible();
      }
    });

    test("finds the Admin entry in the mobile menu at 320 px", async ({ page }) => {
      await page.setViewportSize({ width: 320, height: 640 });
      await signIn(page, ADMIN_EMAIL, ADMIN_PASSWORD!);
      await page.goto("/");

      await page.getByRole("button", { name: "Menu" }).click();
      await expect(page.getByTestId("mobile-admin-link")).toBeVisible();
    });

    test("opens the audit page", async ({ page }) => {
      await signIn(page, ADMIN_EMAIL, ADMIN_PASSWORD!);
      await page.goto("/admin/audit");
      await expect(page.getByRole("heading", { name: "Audit trail" })).toBeVisible();
    });
  });
});
