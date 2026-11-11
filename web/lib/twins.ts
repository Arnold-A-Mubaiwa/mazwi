import { findLanguage } from "./languages.ts";
import { readDraft, type StoryDraft } from "./story-drafts.ts";

export type StoryTwin = {
  storyId: string; storyTitle: string; twinStoryId: string; twinTitle: string; twinArtistName: string;
  twinLanguage: string; twinCommunity: string; status: "suggested" | "confirmed";
};

function isTwin(value: unknown): value is StoryTwin {
  const twin = value as Partial<StoryTwin> | null;
  return !!twin && typeof twin.storyId === "string" && typeof twin.storyTitle === "string" && typeof twin.twinStoryId === "string"
    && typeof twin.twinTitle === "string" && typeof twin.twinArtistName === "string" && !!findLanguage(twin.twinLanguage)
    && typeof twin.twinCommunity === "string" && (twin.status === "suggested" || twin.status === "confirmed");
}

export async function getTwins(signal?: AbortSignal): Promise<StoryTwin[]> {
  const response = await fetch("/api/studio/twins", { cache: "no-store", signal });
  if (response.status === 401) throw new Error("Sign in to see Story Twins.");
  if (!response.ok) throw new Error("Story Twins could not be loaded. Please try again.");
  const value: unknown = await response.json();
  if (!Array.isArray(value) || !value.every(isTwin)) throw new Error("The API returned unexpected Story Twins.");
  return value;
}

export async function decideTwin(storyId: string, twinStoryId: string, decision: "confirm" | "reject"): Promise<void> {
  const response = await fetch(`/api/stories/${encodeURIComponent(storyId)}/twins/${encodeURIComponent(twinStoryId)}/${decision}`, { method: "POST" });
  if (response.status === 204) return;
  if (response.status === 404) throw new Error("This twin is no longer available. One of the stories may have changed or opted out.");
  if (response.status === 403) throw new Error("Only the story’s artist can do this.");
  throw new Error("That did not work. Please try again.");
}

export async function setTwinsAllowed(id: string, allowed: boolean, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/twinning`, {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ allowed }), signal,
  }), id);
}

/** "Also told in Yoruba by ABS (Yoruba, Nigeria)" */
export function twinLine(twin: Pick<StoryTwin, "twinLanguage" | "twinArtistName" | "twinCommunity">): string {
  const language = findLanguage(twin.twinLanguage)?.name ?? twin.twinLanguage;
  return `Told in ${language} by ${twin.twinArtistName} (${twin.twinCommunity})`;
}
