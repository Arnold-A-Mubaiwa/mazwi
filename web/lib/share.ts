export type ShareResult = "shared" | "copied" | "cancelled" | "failed";
type ShareNavigator = { share?: (data: ShareData) => Promise<void>; clipboard?: { writeText(text: string): Promise<void> } };

export function storyShareData(title: string, artistName: string, url: string): ShareData {
  return { title: `${title} | Mazwi`, text: `${title} by ${artistName}, an African story on Mazwi.`, url };
}

// Uses the phone's share sheet when there is one; otherwise copies the link.
export async function shareStory(data: ShareData, browser: ShareNavigator): Promise<ShareResult> {
  if (typeof browser.share === "function") {
    try { await browser.share(data); return "shared"; }
    catch (failure) {
      if (failure instanceof Error && failure.name === "AbortError") return "cancelled";
    }
  }
  try {
    if (!browser.clipboard || !data.url) return "failed";
    await browser.clipboard.writeText(data.url);
    return "copied";
  } catch { return "failed"; }
}

export function shareMessage(result: ShareResult, url: string): string {
  if (result === "shared") return "Thanks for sharing this story.";
  if (result === "copied") return "Link copied. Paste it anywhere to share this story.";
  if (result === "cancelled") return "";
  return `Copy this link to share the story: ${url}`;
}

export type SharedRecording = { file: File };
export const SHARE_CACHE = "mazwi-share";
export const SHARE_KEY = "/shared-recording";

// Reads, then forgets, a recording another app shared into Mazwi (stored by public/sw.js).
export async function takeSharedRecording(storage: CacheStorage | undefined): Promise<File | null> {
  if (!storage) return null;
  const cache = await storage.open(SHARE_CACHE);
  const response = await cache.match(SHARE_KEY);
  if (!response) return null;
  await cache.delete(SHARE_KEY);
  const blob = await response.blob();
  const name = decodeURIComponent(response.headers.get("X-File-Name") ?? "shared-recording");
  return new File([blob], name, { type: blob.type || response.headers.get("Content-Type") || "" });
}
