import { test as setup, expect, type APIRequestContext } from "@playwright/test";
import { readdir, readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { ARTIST, AUTH_FILE, OTHER_ARTIST } from "./artists";

export async function signInOrSignUp(request: APIRequestContext, artist: { email: string; password: string }) {
  const data = { email: artist.email, password: artist.password };
  let response = await request.post("/api/account/signin", { data });
  if (response.status() === 401) response = await request.post("/api/account/signup", { data });
  expect(response.ok()).toBe(true);
}

// Moderators must have a confirmed email, so confirm the fixture's email once through the local outbox.
async function confirmEmail(request: APIRequestContext, email: string) {
  if ((await (await request.get("/api/account/me")).json()).emailVerified) return;
  const sentAfter = Date.now();
  expect((await request.post("/api/account/verification-email")).status()).toBe(202);
  const outbox = resolve("../.local/browser-outbox");
  let token = "";
  await expect.poll(async () => {
    for (const name of (await readdir(outbox).catch(() => [] as string[])).filter(file => file.endsWith(".eml")).sort().reverse()) {
      const content = await readFile(resolve(outbox, name), "utf8");
      const match = content.match(/verify-email\?token=([A-Za-z0-9_-]+)/);
      if (content.includes(`To: ${email}`) && match && Date.parse(content.match(/^Date: (.+)$/m)?.[1] ?? "") >= sentAfter - 2000) { token = match[1]; return true; }
    }
    return false;
  }).toBe(true);
  expect((await request.post("/api/account/verify-email", { data: { token } })).status()).toBe(204);
}

setup("SignIn_BrowserFixtureArtists_SavesSession", async ({ request, playwright }) => {
  const other = await playwright.request.newContext({ baseURL: "http://127.0.0.1:3000" });
  await signInOrSignUp(other, OTHER_ARTIST);
  await confirmEmail(other, OTHER_ARTIST.email);
  await other.dispose();
  await signInOrSignUp(request, ARTIST);
  await request.storageState({ path: AUTH_FILE });
});
