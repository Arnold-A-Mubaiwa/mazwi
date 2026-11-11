import { test as setup, expect, type APIRequestContext } from "@playwright/test";
import { ARTIST, AUTH_FILE, OTHER_ARTIST } from "./artists";

export async function signInOrSignUp(request: APIRequestContext, artist: { email: string; password: string }) {
  const data = { email: artist.email, password: artist.password };
  let response = await request.post("/api/account/signin", { data });
  if (response.status() === 401) response = await request.post("/api/account/signup", { data });
  expect(response.ok()).toBe(true);
}

setup("SignIn_BrowserFixtureArtists_SavesSession", async ({ request, playwright }) => {
  const other = await playwright.request.newContext({ baseURL: "http://127.0.0.1:3000" });
  await signInOrSignUp(other, OTHER_ARTIST);
  await other.dispose();
  await signInOrSignUp(request, ARTIST);
  await request.storageState({ path: AUTH_FILE });
});
