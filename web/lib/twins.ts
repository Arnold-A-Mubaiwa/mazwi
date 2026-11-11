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

export type ViewerTwin = { storyId: string; title: string; artistName: string; community: string; language: string; availableInYourLanguage: boolean };

function isViewerTwin(value: unknown): value is ViewerTwin {
  const twin = value as Partial<ViewerTwin> | null;
  return !!twin && typeof twin.storyId === "string" && typeof twin.title === "string" && typeof twin.artistName === "string"
    && typeof twin.community === "string" && !!findLanguage(twin.language) && typeof twin.availableInYourLanguage === "boolean";
}

/** Confirmed twins of a story in the feed. A failure only hides the card, so it never blocks watching. */
export async function getViewerTwins(id: string, language: string, signal?: AbortSignal): Promise<ViewerTwin[]> {
  const response = await fetch(`/api/feed/${encodeURIComponent(id)}/twins?language=${encodeURIComponent(language)}`, { cache: "no-store", signal });
  if (!response.ok) return [];
  const value: unknown = await response.json();
  return Array.isArray(value) && value.every(isViewerTwin) ? value : [];
}

export async function requestVersion(id: string, language: string): Promise<void> {
  const response = await fetch(`/api/feed/${encodeURIComponent(id)}/version-requests`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ language }),
  });
  if (response.status === 202) return;
  if (response.status === 400) throw new Error("This story is already available in your language, or that language is not supported.");
  if (response.status === 404) throw new Error("This story is no longer in the feed.");
  if (response.status === 429) throw new Error("Too many requests from here. Wait a minute, then try again.");
  throw new Error("Your request could not be sent. Please try again.");
}

export type VersionRequestTotal = { storyId: string; language: string; count: number };

export async function getVersionRequests(signal?: AbortSignal): Promise<VersionRequestTotal[]> {
  const response = await fetch("/api/studio/version-requests", { cache: "no-store", signal });
  if (!response.ok) throw new Error("Listener requests could not be loaded.");
  const value: unknown = await response.json();
  if (!Array.isArray(value) || !value.every(item => item && typeof item.storyId === "string" && !!findLanguage(item.language) && Number.isInteger(item.count) && item.count > 0)) {
    throw new Error("The API returned unexpected listener requests.");
  }
  return value;
}

/** "Listeners asked for a Shona version (3) · a Zulu version (1)" */
export function requestsSummary(totals: VersionRequestTotal[]): string {
  if (!totals.length) return "";
  const named = (code: string) => { const name = findLanguage(code)?.name ?? code; return `${/^[aeiou]/i.test(name) ? "an" : "a"} ${name} version`; };
  return `Listeners asked for ${totals.map(total => `${named(total.language)} (${total.count})`).join(" · ")}`;
}

/** "Also told in Yoruba by ABS (Yoruba, Nigeria)" */
export function twinLine(twin: Pick<StoryTwin, "twinLanguage" | "twinArtistName" | "twinCommunity">): string {
  const language = findLanguage(twin.twinLanguage)?.name ?? twin.twinLanguage;
  return `Told in ${language} by ${twin.twinArtistName} (${twin.twinCommunity})`;
}
