import type { LanguageCode } from "./languages.ts";
import { readDraft, type StoryDraft } from "./story-drafts.ts";

export type SubtitleTiming = { startsMs: number[]; endMs: number };
export type SubtitleCue = { startMs: number; endMs: number; text: string };
export const MAX_SUBTITLE_END_MS = 4 * 60 * 60 * 1000;

// Same rule as the API: every non-empty, trimmed line is one subtitle.
export function subtitleLines(text: string): string[] {
  return text.split("\n").map(line => line.trim()).filter(line => line.length > 0);
}

export function isTiming(value: unknown): value is SubtitleTiming {
  if (!value || typeof value !== "object") return false;
  const timing = value as Record<string, unknown>;
  return Array.isArray(timing.startsMs) && timing.startsMs.every(Number.isInteger) && Number.isInteger(timing.endMs);
}

export function isCue(value: unknown): value is SubtitleCue {
  if (!value || typeof value !== "object") return false;
  const cue = value as Record<string, unknown>;
  return Number.isInteger(cue.startMs) && Number.isInteger(cue.endMs) && typeof cue.text === "string";
}

// Seconds typed by the artist, such as "1.5", become whole milliseconds. Anything else is null.
export function parseSeconds(value: string): number | null {
  const text = value.trim();
  if (!/^\d+(\.\d{1,3})?$/.test(text)) return null;
  return Math.round(Number(text) * 1000);
}

export function formatSeconds(milliseconds: number): string {
  return (milliseconds / 1000).toFixed(1);
}

export function timingError(startsMs: (number | null)[], endMs: number | null, lineCount: number): string | null {
  if (lineCount === 0) return "Save some text before timing subtitles.";
  if (startsMs.length !== lineCount || startsMs.some(start => start === null)) return "Give every line a start time in seconds, such as 1.5.";
  const starts = startsMs as number[];
  if (starts.some((start, index) => index > 0 && start <= starts[index - 1])) return "Each line must start after the line before it.";
  if (endMs === null || endMs <= starts[starts.length - 1]) return "Set an end time after the last line starts.";
  if (endMs > MAX_SUBTITLE_END_MS) return "Subtitles can last up to four hours.";
  return null;
}

export function cueAt(cues: SubtitleCue[] | null | undefined, milliseconds: number): SubtitleCue | null {
  return cues?.find(cue => milliseconds >= cue.startMs && milliseconds < cue.endMs) ?? null;
}

export async function saveSubtitleTiming(id: string, language: LanguageCode, timing: SubtitleTiming, reviewVersion: string, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/subtitles/${language}`, {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...timing, reviewVersion }), signal,
  }), id);
}
