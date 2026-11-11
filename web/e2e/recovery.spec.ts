import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { readdir, readFile } from "node:fs/promises";
import { resolve } from "node:path";

const outbox = resolve("../.local/browser-outbox");
// The newest emailed link for this address, read from the .eml files the API writes on this local demo.
async function latestLink(email: string, path: string): Promise<string> {
  let link: string | undefined;
  await expect.poll(async () => {
    const files = (await readdir(outbox).catch(() => [] as string[])).filter(name => name.endsWith(".eml")).sort();
    for (const name of files.reverse()) {
      const content = await readFile(resolve(outbox, name), "utf8");
      const match = content.match(new RegExp(`http://127\\.0\\.0\\.1:3000${path}\\?token=[A-Za-z0-9_-]+`));
      if (content.includes(`To: ${email}`) && match) { link = match[0]; return true; }
    }
    return false;
  }).toBe(true);
  return link!;
}

test.use({ storageState: { cookies: [], origins: [] } });

test("Recovery_NewArtistConfirmsEmailThenResetsForgottenPassword", async ({ page }) => {
  const email = `recovery-${randomUUID().slice(0, 8)}@mazwi.test`;
  await page.goto("/signup?next=%2Fstudio");
  await page.getByLabel("Email address", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill("first long password");
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page).toHaveURL(/\/studio$/);
  await expect(page.getByRole("region", { name: "Confirm your email" })).toBeVisible();

  // Confirm the email with the link sent at sign-up.
  await page.goto(await latestLink(email, "/verify-email"));
  await expect(page.getByText(/Email confirmed/)).toBeVisible();
  await page.goto("/studio");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByRole("region", { name: "Confirm your email" })).toHaveCount(0);

  // Forget the password and choose a new one.
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await page.goto("/signin");
  await page.getByRole("link", { name: "Forgot your password?", exact: true }).click();
  await expect(page).toHaveURL(/\/forgot-password$/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Forgot your");
  await page.getByLabel("Email address", { exact: true }).fill(email);
  await page.getByRole("button", { name: "Send reset link", exact: true }).click();
  await expect(page.getByText(/If an account uses that email/)).toBeVisible();
  const reset = await latestLink(email, "/reset-password");
  await page.goto(reset);
  await page.getByLabel("New password", { exact: true }).fill("second long password");
  await page.getByLabel("Repeat new password", { exact: true }).fill("not the same password");
  await page.getByRole("button", { name: "Change password", exact: true }).click();
  await expect(page.getByRole("main").getByRole("alert")).toContainText("do not match");
  await page.getByLabel("Repeat new password", { exact: true }).fill("second long password");
  await page.screenshot({ path: "../.local/mazwi-reset-password.png", fullPage: true });
  await page.getByRole("button", { name: "Change password", exact: true }).click();
  await expect(page.getByText(/Your password has changed/)).toBeVisible();

  // The link works once; the old password no longer does; the new one does.
  await page.goto(reset);
  await page.getByLabel("New password", { exact: true }).fill("third long password");
  await page.getByLabel("Repeat new password", { exact: true }).fill("third long password");
  await page.getByRole("button", { name: "Change password", exact: true }).click();
  await expect(page.getByRole("main").getByRole("alert")).toContainText("expired or was already used");
  expect((await page.request.post("/api/account/signin", { data: { email, password: "first long password" } })).status()).toBe(401);
  expect((await page.request.post("/api/account/signin", { data: { email, password: "second long password" } })).status()).toBe(200);
  // Remove the account this test created.
  expect((await page.request.post("/api/account/delete", { data: { password: "second long password" } })).status()).toBe(204);
});

test("Recovery_UnknownEmail_GetsTheSameAnswer", async ({ page }) => {
  await page.goto("/forgot-password");
  await page.getByLabel("Email address", { exact: true }).fill(`nobody-${randomUUID().slice(0, 8)}@mazwi.test`);
  await page.getByRole("button", { name: "Send reset link", exact: true }).click();
  await expect(page.getByText(/If an account uses that email/)).toBeVisible();
});
