import { LANGUAGES, type LanguageCode } from "./languages.ts";

export const PLAY_THRESHOLD_MS = 3000;
// Gaps larger than this between time updates are seeks or stalls, not listening.
const MAX_STEP_MS = 1500;
const MIN_FINISHED_MS = 1000;

export type LanguagePlays = { language: LanguageCode; plays: number; playsLast7Days: number };
export type StoryStats = { storyId: string; plays: number; playsLast7Days: number; languages: LanguagePlays[] };

// Calls onPlay once: after the viewer has actually listened for the threshold, or listened through to the end of a
// shorter recording (with at least a second heard, so seeking to the end does not count). Seeking forward never adds time.
export function createPlayTracker(onPlay: () => void, threshold = PLAY_THRESHOLD_MS) {
  let listened = 0;
  let last: number | null = null;
  let done = false;
  return {
    update(currentMs: number, playing: boolean) {
      if (done) return;
      if (playing && last !== null) {
        const step = currentMs - last;
        if (step > 0 && step <= MAX_STEP_MS) listened += step;
      }
      last = currentMs;
      if (listened >= threshold) { done = true; onPlay(); }
    },
    finish() {
      if (!done && listened >= MIN_FINISHED_MS) { done = true; onPlay(); }
    },
  };
}

// Fire and forget: counting a play must never interrupt the viewer.
export async function recordPlay(id: string, language: LanguageCode, version: string): Promise<boolean> {
  try {
    const response = await fetch(`/api/feed/${encodeURIComponent(id)}/plays`, {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ language, version }), keepalive: true,
    });
    return response.status === 204;
  } catch { return false; }
}

function isCount(value: unknown): value is number { return Number.isInteger(value) && (value as number) >= 0; }

export async function getStudioStats(signal?: AbortSignal): Promise<Map<string, StoryStats>> {
  const response = await fetch("/api/studio/stats", { cache: "no-store", signal });
  if (!response.ok) throw new Error("Play counts could not be loaded.");
  const value: unknown = await response.json();
  const valid = Array.isArray(value) && value.every(item => item && typeof item.storyId === "string" && isCount(item.plays) && isCount(item.playsLast7Days)
    && Array.isArray(item.languages) && item.languages.every((entry: LanguagePlays) => entry && LANGUAGES.some(language => language.code === entry.language)
      && isCount(entry.plays) && isCount(entry.playsLast7Days)));
  if (!valid) throw new Error("The API returned unexpected play counts.");
  return new Map((value as StoryStats[]).map(stats => [stats.storyId, stats]));
}

export function playsSummary(stats: StoryStats | undefined, name: (code: string) => string): string {
  if (!stats || stats.plays === 0) return "No plays yet";
  const total = `${stats.plays.toLocaleString("en")} ${stats.plays === 1 ? "play" : "plays"} · ${stats.playsLast7Days.toLocaleString("en")} this week`;
  return stats.languages.length > 1 ? `${total} · ${stats.languages.map(entry => `${name(entry.language)} ${entry.plays.toLocaleString("en")}`).join(" · ")}` : total;
}
