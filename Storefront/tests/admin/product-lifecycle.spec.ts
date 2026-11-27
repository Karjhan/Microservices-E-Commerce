import { randomUUID } from "node:crypto";
import { expect, test, type Page } from "@playwright/test";

/**
 * US2: an administrator creates a draft with two images and a model, publishes it, edits the price and deletes it.
 * Needs the development admin account: set E2E_ADMIN_PASSWORD (and E2E_ADMIN_EMAIL if it is not admin@store.local).
 */
const GATEWAY_URL = process.env.NEXT_PUBLIC_API_GATEWAY_URL ?? "http://localhost:8080";
const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL ?? "admin@store.local";
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD;

// 1x1 transparent PNG: a real image, so the server's content check and the browser both accept it.
const PNG = Buffer.from(
  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",
  "base64"
);

// Smallest useful ASCII STL: one triangle.
const STL = Buffer.from(
  "solid tri\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 10 0 0\nvertex 0 10 0\nendloop\nendfacet\nendsolid tri\n"
);

async function signIn(page: Page) {
  await page.goto("/login");
  await page.fill("#email", ADMIN_EMAIL);
  await page.fill("#password", ADMIN_PASSWORD!);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await page.waitForURL((url) => !url.pathname.startsWith("/login"));
}

async function shopSearch(page: Page, name: string) {
  await page.goto("/products");
  await page.getByPlaceholder("Search products...").fill(name);
  await page.getByPlaceholder("Search products...").press("Enter");
}

test.describe("product lifecycle", () => {
  test.skip(!ADMIN_PASSWORD, "Set E2E_ADMIN_PASSWORD to the development admin password.");
  // One product moves through the whole flow, so the steps cannot run in parallel or on every viewport.
  test.describe.configure({ mode: "serial" });

  test("create a draft with media, publish, edit the price and delete", async ({ page, request }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-1280", "The flow is viewport independent; run it once.");

    const name = `E2E Lifecycle ${Date.now()}`;
    await signIn(page);

    // --- create a draft with two images and a model ---------------------------------------------------------------
    await page.goto("/admin/products/new");
    await page.fill("#name", name);
    await page.fill("#shortDescription", "End-to-end short description");
    await page.fill("#longDescription", "End-to-end long description");
    await page.fill("#price", "9.99");
    await page.fill("#categoryId", randomUUID());
    await page.getByRole("button", { name: "PLA", exact: true }).click();
    await page.getByRole("button", { name: "FDM", exact: true }).click();

    await page.setInputFiles("#new-product-images", [
      { name: "front.png", mimeType: "image/png", buffer: PNG },
      { name: "back.png", mimeType: "image/png", buffer: PNG },
    ]);
    await page.setInputFiles("#new-product-model", { name: "triangle.stl", mimeType: "model/stl", buffer: STL });
    await expect(page.getByTestId("staged-images").getByRole("listitem")).toHaveCount(2);

    await page.getByRole("button", { name: "Create Product", exact: true }).click();
    await page.waitForURL(/\/admin\/products\/[0-9a-f-]{36}$/);
    const productId = page.url().split("/").pop()!;

    // --- the draft carries its media and is not visible to shoppers ------------------------------------------------
    await expect(page.getByTestId("product-status")).toHaveText("Draft");
    await expect(page.getByTestId("image-grid").getByRole("listitem")).toHaveCount(2);
    await expect(page.getByTestId("primary-badge")).toHaveCount(1);
    await expect(page.getByTestId("model-info")).toContainText("triangle.stl");

    const hiddenDraft = await request.get(`${GATEWAY_URL}/products/${productId}`);
    expect(hiddenDraft.status()).toBe(404);

    // --- publish: it appears in the shop listing -------------------------------------------------------------------
    await page.getByRole("button", { name: "Publish", exact: true }).click();
    await expect(page.getByTestId("product-status")).toHaveText("Active");

    await shopSearch(page, name);
    await expect(page.getByRole("link", { name })).toBeVisible();

    // --- edit the price --------------------------------------------------------------------------------------------
    await page.goto(`/admin/products/${productId}`);
    await page.fill("#price", "12.34");
    await page.getByRole("button", { name: "Update Product", exact: true }).click();
    await expect(page.getByText("Product saved.")).toBeVisible();

    await page.goto(`/products/${productId}`);
    await expect(page.getByText(/12[.,]34/).first()).toBeVisible();

    // --- delete: it disappears from the listing --------------------------------------------------------------------
    await page.goto(`/admin/products/${productId}`);
    await page.getByRole("button", { name: "Delete Product", exact: true }).click();
    await page.getByRole("button", { name: "Delete product", exact: true }).click();
    await page.waitForURL(/\/admin\/products$/);

    await shopSearch(page, name);
    await expect(page.getByRole("link", { name })).toHaveCount(0);
    expect((await request.get(`${GATEWAY_URL}/products/${productId}`)).status()).toBe(404);
  });

  test("invalid fields are explained next to the field", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-1280", "The flow is viewport independent; run it once.");
    await signIn(page);

    await page.goto("/admin/products/new");
    await page.fill("#name", "x");
    await page.fill("#price", "-1");
    await page.getByRole("button", { name: "Create Product", exact: true }).click();

    await expect(page.getByText("Name must be at least 2 characters")).toBeVisible();
    await expect(page.getByText("Price cannot be negative")).toBeVisible();
    await expect(page).toHaveURL(/\/admin\/products\/new$/);
  });

  test("a file that is not an image is refused before upload", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "desktop-1280", "The flow is viewport independent; run it once.");
    await signIn(page);

    await page.goto("/admin/products/new");
    await page.setInputFiles("#new-product-images", {
      name: "malware.exe",
      mimeType: "application/octet-stream",
      buffer: Buffer.from("MZ"),
    });

    await expect(page.getByRole("alert").filter({ hasText: "malware.exe" })).toBeVisible();
    await expect(page.getByTestId("staged-images")).toHaveCount(0);
  });
});
