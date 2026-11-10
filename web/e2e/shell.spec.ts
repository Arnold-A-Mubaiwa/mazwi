import { test, expect } from "@playwright/test";
test("Home_NavigationAndPreference_WorksAfterReload", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  await page.goto("/");
  await expect(page.getByRole("heading", { name: /Every African story/ })).toBeVisible();
  await expect(page.getByRole("heading", { name: /first voices/ })).toBeVisible();
  await expect(page.getByRole("link", { name: "Upload", exact: true })).toHaveAttribute("href", "/upload");
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("xh");
  await expect(page.getByText("Molo. Make yourself at home.")).toBeVisible();
  await page.reload();
  await expect(page.getByRole("combobox", { name: "Your language", exact: true })).toHaveValue("xh");
  await page.getByRole("link", { name: /Our story/ }).click();
  await expect(page.getByRole("heading", { name: /Stories belong/ })).toBeVisible();
  await page.getByRole("link", { name: "Mazwi home" }).click();
  await expect(page.getByRole("combobox", { name: "Your language", exact: true })).toHaveValue("xh");
  expect(errors).toEqual([]);
});
test("Health_RealBackendThroughNextProxy_ReturnsMazwi", async ({ page, request }) => {
  const response = await request.get("/api/health");
  expect(response.status()).toBe(200);
  expect(await response.json()).toEqual({ status: "ok", service: "Mazwi" });
  await page.goto("/status");
  await expect(page.getByRole("status")).toHaveText("Mazwi API is reachable.");
  await page.getByRole("button", { name: "Check again" }).click();
  await expect(page.getByRole("status")).toHaveText("Mazwi API is reachable.");
});
test("Mobile_NarrowScreenAndLanguageControl_NoHorizontalOverflow", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");
  await expect(page.getByRole("heading", { name: /Every African story/ })).toBeVisible();
  await page.getByRole("combobox", { name: "Your language", exact: true }).selectOption("sn");
  await expect(page.getByText("Mhoro. Make yourself at home.")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: "../.local/mazwi-mobile.png", fullPage: true });
});
test("Desktop_Shell_HasReadableLayout", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.goto("/");
  await expect(page.getByRole("main")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.screenshot({ path: "../.local/mazwi-desktop.png", fullPage: true });
});
test("Health_UnavailableApi_ShowsRecoverableError", async ({ page }) => {
  await page.route("**/api/health", (route) => route.fulfill({ status: 503, contentType: "application/json", body: "{}" }));
  await page.goto("/status");
  await expect(page.getByRole("status")).toHaveText("Mazwi API is not reachable.");
  await expect(page.getByRole("button", { name: "Check again" })).toBeEnabled();
});
