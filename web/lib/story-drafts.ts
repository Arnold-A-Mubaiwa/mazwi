import { LANGUAGES, type LanguageCode } from "./languages.ts";

export const MAX_STORY_TEXT = 20_000;
export type TranslationDraft = { language: LanguageCode; text: string; status: "draft" };
export type StoryDraft = {
  id: string; title: string; artistName: string; language: LanguageCode; status: "unpublished";
  transcript: string | null; translations: TranslationDraft[];
};

export function storyTextError(text: string): string | null {
  const value = text.trim();
  if (!value || value.length > MAX_STORY_TEXT) return "Enter 1–20,000 characters of story text.";
  if (/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f-\u009f]/.test(value)) return "Remove control characters. Line breaks and tabs are allowed.";
  return null;
}

function isLanguage(value: unknown): value is LanguageCode {
  return LANGUAGES.some(language => language.code === value);
}

function isTranslation(value: unknown): value is TranslationDraft {
  if (!value || typeof value !== "object") return false;
  const draft = value as Record<string, unknown>;
  return isLanguage(draft.language) && typeof draft.text === "string" && draft.status === "draft";
}

async function readDraft(response: Response, id: string): Promise<StoryDraft> {
  if (!response.ok) {
    if (response.status === 404) throw new Error("This story could not be found. Check the editing link or upload a story first.");
    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      throw new Error(typeof problem?.detail === "string" ? problem.detail.slice(0, 300) : "Check the text and translation language, then try again.");
    }
    throw new Error("We couldn’t reach your saved story. Please try again. Any text you entered is still here.");
  }
  const draft = await response.json();
  if (!draft || typeof draft.id !== "string" || draft.id.toLowerCase() !== id.toLowerCase()
    || typeof draft.title !== "string" || typeof draft.artistName !== "string" || !isLanguage(draft.language)
    || draft.status !== "unpublished" || (draft.transcript !== null && typeof draft.transcript !== "string")
    || !Array.isArray(draft.translations) || !draft.translations.every(isTranslation)) {
    throw new Error("The API returned an unexpected story. Your edits have not been confirmed saved.");
  }
  return draft;
}

export async function getStoryDraft(id: string, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/drafts`, { cache: "no-store", signal }), id);
}

export async function saveStoryText(id: string, section: "transcript" | LanguageCode, text: string, signal?: AbortSignal): Promise<StoryDraft> {
  const invalid = storyTextError(text);
  if (invalid) throw new Error(invalid);
  const suffix = section === "transcript" ? "transcript" : `translations/${encodeURIComponent(section)}`;
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/drafts/${suffix}`, {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ text }), signal,
  }), id);
}
