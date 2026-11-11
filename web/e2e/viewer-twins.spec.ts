import { test, expect, type APIRequestContext } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { OTHER_ARTIST } from "./artists";

// A viewer moves from a Shona story to its Yoruba twin, asks for it in Shona, and later hears it in Shona.
// Real API, database and background sweep; only OpenAI is replaced by e2e/fake-openai.mjs.
async function publish(request: APIRequestContext, title: string, language: string, words: string): Promise<string> {
  const created = await request.post("/api/stories", { multipart: {
    title, artistName: "Mazwi browser fixture", community: "Twins test", country: "ZW", type: "folktale", language, permission: "true",
    recording: { name: "clip.wav", mimeType: "audio/wav", buffer: Buffer.from("RIFF0000WAVEdata0000") },
  } });
  expect(created.status()).toBe(201);
  const { id } = await created.json();
  expect((await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: words } })).ok()).toBe(true);
  const draft = await (await request.get(`/api/stories/${id}/drafts`)).json();
  expect((await request.post(`/api/stories/${id}/approvals/${language}`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);
  return id;
}

async function remove(request: APIRequestContext, id: string) {
  const draft = await (await request.get(`/api/stories/${id}/drafts`)).json();
  expect((await request.delete(`/api/stories/${id}?version=${draft.reviewVersion}`)).status()).toBe(204);
}

test("ViewerTwins_ShonaViewerFindsYorubaTwinAsksForShonaThenHearsIt", async ({ request, browser }) => {
  test.setTimeout(120000);
  const words = ["Tsuro", "Gudo", ...Array.from({ length: 7 }, () => randomUUID().slice(0, 8))].join(" ");
  const run = randomUUID().slice(0, 8);
  const shonaTitle = `Tsuro na Gudo ${run}`, yorubaTitle = `Ìjàpá àti Ọ̀bọ ${run}`;
  const otherContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const other = await otherContext.newPage();
  await other.goto("/signin?next=%2Fstudio");
  await other.getByLabel("Email address", { exact: true }).fill(OTHER_ARTIST.email);
  await other.getByLabel("Password", { exact: true }).fill(OTHER_ARTIST.password);
  await other.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(other).toHaveURL(/\/studio$/);
  const viewerContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const viewer = await viewerContext.newPage();
  let shona = "", yoruba = "";
  try {
    shona = await publish(request, shonaTitle, "sn", `${words} rwizi`);
    yoruba = await publish(other.request, yorubaTitle, "yo", `${words} odo`);
    // The sweep suggests the pair; the Shona artist confirms it.
    await expect.poll(async () => (await request.post(`/api/stories/${shona}/twins/${yoruba}/confirm`)).status(), { timeout: 45000 }).toBe(204);

    // A Shona viewer finds the Yoruba telling and asks for a Shona version.
    await viewer.goto(`/story/${shona}`);
    const card = viewer.getByTestId("viewer-twin").filter({ hasText: yorubaTitle });
    await expect(card).toContainText(`Also told in Yoruba by Mazwi browser fixture (Twins test): ${yorubaTitle}`);
    await expect(card).toContainText("has not approved a Shona version yet");
    await expect(card.getByRole("link", { name: "Hear it in Yoruba ↗" })).toHaveAttribute("href", `/story/${yoruba}`);
    await card.getByRole("button", { name: "Ask for a Shona version" }).click();
    await expect(card.getByRole("status")).toHaveText("Thank you. Mazwi browser fixture will see that listeners asked for a Shona version.");

    // The Yoruba artist sees the request in Studio, then approves a Shona version.
    await other.goto("/studio");
    const studioCard = other.locator(".studio-card").filter({ hasText: yorubaTitle });
    await expect(studioCard.getByTestId("studio-requests")).toHaveText("Listeners asked for a Shona version (1)");
    expect((await other.request.put(`/api/stories/${yoruba}/drafts/translations/sn`, { data: { text: `${words} rwizi` } })).ok()).toBe(true);
    const draft = await (await other.request.get(`/api/stories/${yoruba}/drafts`)).json();
    expect((await other.request.post(`/api/stories/${yoruba}/approvals/sn`, { data: { reviewVersion: draft.reviewVersion } })).ok()).toBe(true);
    await other.reload();
    await expect(studioCard).toBeVisible();
    await expect(studioCard.getByTestId("studio-requests")).toHaveCount(0);

    // The viewer now hears the twin in Shona.
    await viewer.reload();
    await card.getByRole("link", { name: "Hear it in Shona ↗" }).click();
    await expect(viewer).toHaveURL(new RegExp(`/story/${yoruba}$`));
    await expect(viewer.getByRole("heading", { name: yorubaTitle })).toBeVisible();
    await expect(viewer.getByText(/Approved Shona text with original Yoruba audio/)).toBeVisible();
    await expect(viewer.getByTestId("viewer-twin").filter({ hasText: shonaTitle })).toContainText("Also told in Shona");
  } finally {
    if (shona) await remove(request, shona);
    if (yoruba) await remove(other.request, yoruba);
    await otherContext.close();
    await viewerContext.close();
  }
});
