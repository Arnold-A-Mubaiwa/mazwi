import { test, expect, type APIRequestContext, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { OTHER_ARTIST } from "./artists";

// Story Twins against the real API, database and background sweep. Only OpenAI is replaced, by the local
// e2e/fake-openai.mjs, whose embeddings make stories that share most words similar.
function shared(): string {
  return ["Tsuro", "Gudo", ...Array.from({ length: 7 }, () => randomUUID().slice(0, 8))].join(" ");
}

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

async function studioShows(page: Page, testId: string, title: string) {
  const card = page.getByTestId(testId).filter({ hasText: title });
  await expect(async () => {
    await page.goto("/studio");
    await expect(card).toBeVisible({ timeout: 2000 });
  }).toPass({ timeout: 45000 });
  return card;
}

test("Twins_SuggestConfirmOptOutAndUnlink_RealSweepWithFakeOpenAi", async ({ page, request, browser }) => {
  const words = shared();
  const run = randomUUID().slice(0, 8);
  const shonaTitle = `Tsuro na Gudo ${run}`, yorubaTitle = `Ìjàpá àti Ọ̀bọ ${run}`, hausaTitle = `Kura da Zaki ${run}`;
  const otherContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const other = await otherContext.newPage();
  await other.goto("/signin?next=%2Fstudio");
  await other.getByLabel("Email address", { exact: true }).fill(OTHER_ARTIST.email);
  await other.getByLabel("Password", { exact: true }).fill(OTHER_ARTIST.password);
  await other.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(other).toHaveURL(/\/studio$/);
  const ids: { request: APIRequestContext; id: string }[] = [];
  try {
    ids.push({ request, id: await publish(request, shonaTitle, "sn", `${words} rwizi`) });
    const yoruba = await publish(other.request, yorubaTitle, "yo", `${words} odo`);
    ids.push({ request: other.request, id: yoruba });
    ids.push({ request: other.request, id: await publish(other.request, hausaTitle, "ha", `${words} kogi`) });

    // The sweep suggests both; nothing is visible to viewers yet.
    const suggestion = await studioShows(page, "twin-suggestion", yorubaTitle);
    await expect(suggestion).toContainText(`Your story ${shonaTitle} may be the same tale as ${yorubaTitle}.`);
    await expect(suggestion).toContainText("Told in Yoruba by Mazwi browser fixture (Twins test)");
    await expect(suggestion.getByRole("link", { name: /Listen first/ })).toHaveAttribute("href", `/story/${yoruba}`);

    // Not the same: the Hausa suggestion goes away for good.
    await page.getByTestId("twin-suggestion").filter({ hasText: hausaTitle }).getByRole("button", { name: "Not the same" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText(`Marked “${hausaTitle}” as not the same story.`);
    await expect(page.getByTestId("twin-suggestion").filter({ hasText: hausaTitle })).toHaveCount(0);

    // Confirm the Yoruba twin; the other artist sees it linked too.
    await suggestion.getByRole("button", { name: "Yes, same story" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText(`Linked “${shonaTitle}” with “${yorubaTitle}”.`);
    await expect(page.getByTestId("twin-confirmed").filter({ hasText: yorubaTitle })).toBeVisible();
    await expect(await studioShows(other, "twin-confirmed", shonaTitle)).toContainText("Told in Shona by Mazwi browser fixture");

    // The other artist opts the Yoruba story out: the twin disappears, then returns when they opt back in.
    await other.goto(`/story/${yoruba}/review`);
    const allow = other.getByLabel("Let Mazwi suggest twins for this story");
    await expect(allow).toBeChecked();
    await allow.click();
    await expect(other.getByRole("main").getByRole("status")).toContainText("Story Twins are off for this story.");
    await expect(allow).not.toBeChecked();
    await page.goto("/studio");
    await expect(page.getByRole("heading", { name: "The same story, another voice." })).toBeVisible();
    await expect(page.getByTestId("twin-confirmed").filter({ hasText: yorubaTitle })).toHaveCount(0);
    await allow.click();
    await expect(other.getByRole("main").getByRole("status")).toContainText("Mazwi may suggest Story Twins for this story.");
    const linked = await studioShows(page, "twin-confirmed", yorubaTitle);

    // Unlink is final.
    await linked.getByRole("button", { name: "Unlink" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText(`Unlinked “${yorubaTitle}”.`);
    await page.reload();
    await expect(page.getByTestId("twin-confirmed").filter({ hasText: yorubaTitle })).toHaveCount(0);
    await expect(page.getByTestId("twin-suggestion").filter({ hasText: yorubaTitle })).toHaveCount(0);
  } finally {
    for (const item of ids) await remove(item.request, item.id);
    await otherContext.close();
  }
});
