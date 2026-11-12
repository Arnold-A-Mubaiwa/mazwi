import { test, expect, type Page } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { OTHER_ARTIST } from "./artists";

// Level 4 journey through the UI, against the real API, database, files and background twin sweep. Only OpenAI is
// replaced, by the local e2e/fake-openai.mjs:
// a Yoruba artist uploads in Yoruba (a new language) → a Shona artist tells the same tale → Mazwi suggests them as twins
// → the Shona artist confirms → a Shona viewer finds the Yoruba telling and asks for Shona → the Yoruba artist sees the
// request, translates into Shona and approves → the viewer hears the twin in Shona.
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8); b.writeUInt32LE(16, 16);
  b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24); b.writeUInt32LE(32000, 28);
  b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40); return b;
}

async function uploadAndApprove(page: Page, title: string, language: string, words: string): Promise<string> {
  await page.goto("/upload");
  await page.getByLabel("Your recording", { exact: true }).setInputFiles({ name: "original.wav", mimeType: "audio/wav", buffer: wav() });
  await page.getByLabel("Story title", { exact: true }).fill(title);
  await page.getByLabel("Artist name", { exact: true }).fill("Mazwi browser fixture");
  await page.getByLabel("Community", { exact: true }).fill("Level 4");
  await page.getByLabel("Country code", { exact: true }).fill("NG");
  await page.getByRole("combobox", { name: "Original language" }).selectOption(language);
  await page.getByRole("checkbox").check();
  const uploaded = page.waitForResponse(response => response.url().endsWith("/api/stories") && response.request().method() === "POST");
  await page.getByRole("button", { name: "Save my story", exact: true }).click();
  const { id } = await (await uploaded).json();
  await page.getByRole("link", { name: /Write transcript and translations/ }).click();
  await page.getByLabel("Original transcript", { exact: true }).fill(words);
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();
  return id;
}

async function removeStory(page: Page, id: string) {
  const draft = await (await page.request.get(`/api/stories/${id}/drafts`)).json();
  expect((await page.request.delete(`/api/stories/${id}?version=${draft.reviewVersion}`)).status()).toBe(204);
}

test("Level4_YorubaAndShonaTwins_ViewerAsksForShonaAndHearsIt", async ({ page, browser }) => {
  test.setTimeout(180000);
  const run = randomUUID().slice(0, 8);
  const words = ["Ijapa", "Tsuro", ...Array.from({ length: 7 }, () => randomUUID().slice(0, 8))].join(" ");
  const yorubaTitle = `Ìjàpá àti Ọ̀bọ ${run}`, shonaTitle = `Tsuro na Gudo ${run}`;
  const shonaContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const shonaArtist = await shonaContext.newPage();
  const viewerContext = await browser.newContext({ storageState: { cookies: [], origins: [] }, baseURL: "http://127.0.0.1:3000" });
  const viewer = await viewerContext.newPage();
  let yoruba = "", shona = "";
  try {
    // A Yoruba artist uploads in Yoruba; the editor warns that AI quality in Yoruba is untested.
    yoruba = await uploadAndApprove(page, yorubaTitle, "yo", `${words} odo`);
    await expect(page.getByTestId("ai-quality-notice").first()).toContainText("Mazwi has not checked AI quality in Yoruba yet.");

    // A Shona artist tells the same tale.
    await shonaArtist.goto("/signin?next=%2Fstudio");
    await shonaArtist.getByLabel("Email address", { exact: true }).fill(OTHER_ARTIST.email);
    await shonaArtist.getByLabel("Password", { exact: true }).fill(OTHER_ARTIST.password);
    await shonaArtist.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(shonaArtist).toHaveURL(/\/studio$/);
    shona = await uploadAndApprove(shonaArtist, shonaTitle, "sn", `${words} rwizi`);

    // Mazwi suggests the twin in Studio; the Shona artist listens first, then confirms.
    const suggestion = shonaArtist.getByTestId("twin-suggestion").filter({ hasText: yorubaTitle });
    await expect(async () => {
      await shonaArtist.goto("/studio");
      await expect(suggestion).toBeVisible({ timeout: 2000 });
    }).toPass({ timeout: 60000 });
    await expect(suggestion).toContainText("Told in Yoruba by Mazwi browser fixture (Level 4)");
    await suggestion.getByRole("button", { name: "Yes, same story" }).click();
    await expect(shonaArtist.getByTestId("twin-confirmed").filter({ hasText: yorubaTitle })).toBeVisible();

    // A Shona viewer finds the Yoruba telling and asks for a Shona version.
    await viewer.goto(`/story/${shona}`);
    const twin = viewer.getByTestId("viewer-twin").filter({ hasText: yorubaTitle });
    await expect(twin).toContainText(`Also told in Yoruba by Mazwi browser fixture (Level 4): ${yorubaTitle}`);
    await twin.getByRole("button", { name: "Ask for a Shona version" }).click();
    await expect(twin.getByRole("status")).toContainText("listeners asked for a Shona version");

    // The Yoruba artist sees the request, translates into Shona in the editor and approves it.
    await page.goto("/studio");
    await expect(page.locator(".studio-card").filter({ hasText: yorubaTitle }).getByTestId("studio-requests")).toHaveText("Listeners asked for a Shona version (1)");
    await page.goto(`/story/${yoruba}/review`);
    await page.getByLabel("Translation language", { exact: true }).selectOption("sn");
    await expect(page.getByTestId("ai-quality-notice").filter({ hasText: "in Shona" })).toHaveCount(0);
    await page.getByLabel("Translation text", { exact: true }).fill(`${words} rwizi`);
    await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("Shona translation saved");
    await page.getByRole("button", { name: "Approve translation", exact: true }).click();
    await expect(page.getByText("Not checked by a speaker of Shona. Viewers will see this.")).toBeVisible();

    // The viewer now hears the twin in Shona.
    await viewer.reload();
    await twin.getByRole("link", { name: "Hear it in Shona ↗" }).click();
    await expect(viewer).toHaveURL(new RegExp(`/story/${yoruba}$`));
    await expect(viewer.getByText(/Approved Shona text with original Yoruba audio/)).toBeVisible();
  } finally {
    if (yoruba) await removeStory(page, yoruba);
    if (shona) await removeStory(shonaArtist, shona);
    await shonaContext.close();
    await viewerContext.close();
  }
});
