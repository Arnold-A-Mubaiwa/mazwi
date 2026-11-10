import { test, expect } from "@playwright/test";
import { randomUUID } from "node:crypto";
import { unlink } from "node:fs/promises";
import { resolve } from "node:path";

const fixtures: string[] = [];
const mediaIds: string[] = [];
function wav() {
  const b = Buffer.alloc(32044); b.write("RIFF"); b.writeUInt32LE(b.length - 8, 4); b.write("WAVEfmt ", 8);
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(16000, 24);
  b.writeUInt32LE(32000, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34); b.write("data", 36); b.writeUInt32LE(b.length - 44, 40);
  return b;
}
test.afterEach(async ({ request }) => {
  for (const id of fixtures.splice(0)) {
    const revoked = await request.put(`/api/stories/${id}/drafts/transcript`, { data: { text: `Closed synthetic fixture ${randomUUID()}` } });
    expect(revoked.ok()).toBe(true); mediaIds.push(id);
  }
  for (const id of mediaIds.splice(0)) if (/^[0-9a-f-]{36}$/i.test(id)) await unlink(resolve(`../.local/browser-media/${id.replaceAll("-", "")}.wav`)).catch(() => {});
});
test("Review_RealRecordingApprovalAndReplacement_RequiresNewApproval", async ({ page, request }) => {
  const uploaded = await request.post("/api/stories", { multipart: {
    title: "Artist review fixture", artistName: "Mazwi browser fixture", community: "Shona", country: "ZW", type: "story", language: "sn", permission: "true",
    recording: { name: "original.wav", mimeType: "audio/wav", buffer: wav() },
  } });
  expect(uploaded.status()).toBe(201); const { id } = await uploaded.json(); fixtures.push(id);
  await page.goto(`/story/${id}/review`);
  await expect(page.locator("audio").first()).toHaveAttribute("controls", "");
  const range = await request.get(`/api/stories/${id}/recordings/sn`, { headers: { Range: "bytes=0-3" } });
  expect(range.status()).toBe(206); expect(await range.body()).toEqual(Buffer.from("RIFF"));
  await page.getByLabel("Original transcript", { exact: true }).fill("Original approved words");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Original transcript saved");
  await page.getByRole("button", { name: "Approve original", exact: true }).click();
  await expect(page.getByRole("button", { name: "Original is approved", exact: true })).toBeDisabled();
  await page.getByLabel("Translation text", { exact: true }).fill("Approved translated words");
  await page.getByRole("button", { name: "Save translation draft", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("translation saved as a draft");
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  await page.getByLabel("Translated recording", { exact: true }).setInputFiles({ name: "translated.wav", mimeType: "audio/wav", buffer: wav() });
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Save translated recording", exact: true }).click();
  await expect(page.getByRole("main").getByRole("status")).toContainText("Translated recording saved as a draft");
  await expect(page.getByRole("button", { name: "Approve translation", exact: true })).toBeEnabled();
  const saved = await (await request.get(`/api/stories/${id}/drafts`)).json();
  mediaIds.push(saved.translations[0].recordingId);
  await page.getByRole("button", { name: "Approve translation", exact: true }).click();
  await expect(page.getByRole("button", { name: "Translation is approved", exact: true })).toBeDisabled();
  await page.getByLabel("Original transcript", { exact: true }).fill("Changed source words");
  await page.getByRole("button", { name: "Save original transcript", exact: true }).click();
  await expect(page.getByRole("button", { name: "Approve original", exact: true })).toBeEnabled();
  const changed = await (await request.get(`/api/stories/${id}/drafts`)).json();
  expect(changed.originalApproved).toBe(false); expect(changed.translations[0].status).toBe("draft");
});
