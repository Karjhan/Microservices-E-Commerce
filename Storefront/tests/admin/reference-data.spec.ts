import { expect, test, type Page } from "@playwright/test";

const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? "admin@store.local";
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD;

async function signIn(page: Page) {
  await page.goto("/login");
  await page.fill("#email", ADMIN_EMAIL);
  await page.fill("#password", ADMIN_PASSWORD!);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/login"));
}

function row(page: Page, name: string) {
  return page.getByTestId("reference-row").filter({ hasText: name });
}

test.describe("reference data", () => {
  test.skip(!ADMIN_PASSWORD, "Set E2E_ADMIN_PASSWORD to the development admin password.");
  test.describe.configure({ mode: "serial" });

  test("a brand can be added, refused as a duplicate, renamed, deactivated and deleted", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-1280", "The flow is viewport independent; run it once.");

    const name = `E2E Brand ${Date.now()}`;
    const renamed = `${name} Renamed`;
    await signIn(page);
    await page.goto("/admin/brands");

    await page.getByRole("button", { name: "Add brand", exact: true }).click();
    await page.fill("#reference-name", name);
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(row(page, name)).toBeVisible();

    await page.getByRole("button", { name: "Add brand", exact: true }).click();
    await page.fill("#reference-name", `  ${name.toUpperCase()}  `);
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(page.getByText("Name must be unique.")).toBeVisible();
    await page.getByRole("button", { name: "Cancel", exact: true }).click();

    await page.getByRole("button", { name: `Edit ${name}`, exact: true }).click();
    await page.fill("#reference-name", renamed);
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(row(page, renamed)).toBeVisible();

    await row(page, renamed).getByRole("button", { name: "Deactivate", exact: true }).click();
    await expect(row(page, renamed).getByText("Inactive")).toBeVisible();

    await page.goto("/admin/products/new");
    await expect(page.locator("#brandId").locator("option", { hasText: renamed })).toHaveCount(0);

    await page.goto("/admin/brands");
    await page.getByRole("button", { name: `Delete ${renamed}`, exact: true }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(row(page, renamed)).toHaveCount(0);
  });

  test("a category in use cannot be deleted, it can be deactivated instead", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-1280", "The flow is viewport independent; run it once.");

    const category = `E2E Category ${Date.now()}`;
    const productName = `E2E Reference Product ${Date.now()}`;
    await signIn(page);

    await page.goto("/admin/categories");
    await page.getByRole("button", { name: "Add category", exact: true }).click();
    await page.fill("#reference-name", category);
    await page.getByRole("button", { name: "Save", exact: true }).click();
    await expect(row(page, category)).toBeVisible();

    await page.goto("/admin/products/new");
    await page.fill("#name", productName);
    await page.fill("#shortDescription", "Short");
    await page.fill("#longDescription", "Long");
    await page.fill("#price", "5");
    await page.selectOption("#categoryId", { label: category });
    await page.getByRole("button", { name: "PLA", exact: true }).click();
    await page.getByRole("button", { name: "FDM", exact: true }).click();
    await page.getByRole("button", { name: "Create Product", exact: true }).click();
    await page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/);
    const productUrl = page.url();

    await page.goto("/admin/categories");
    await page.getByRole("button", { name: `Delete ${category}`, exact: true }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(page.getByText(/used by 1 product/)).toBeVisible();

    await page.getByRole("button", { name: "Deactivate instead", exact: true }).click();
    await expect(row(page, category).getByText("Inactive")).toBeVisible();
    await page.goto(productUrl);
    await expect(page.locator("#categoryId")).toHaveValue(/[0-9a-f-]{36}/);
    await expect(page.locator("#categoryId option:checked")).toContainText(category);

    await page.getByRole("button", { name: "Delete Product", exact: true }).click();
    await page.getByRole("button", { name: "Delete product", exact: true }).click();
    await page.waitForURL(/\/admin\/products$/);
    await page.goto("/admin/categories");
    await page.getByRole("button", { name: `Delete ${category}`, exact: true }).click();
    await page.getByRole("button", { name: "Delete", exact: true }).click();
    await expect(row(page, category)).toHaveCount(0);
  });
});
