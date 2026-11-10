import { LANGUAGES, type LanguageCode } from "./languages.ts";

export const MAX_STORY_TEXT = 20_000;
export const MAX_VOICE_TEXT = 1_800;
export type TranslationDraft = { language: LanguageCode; text: string; status: "draft" | "approved"; recordingId?: string | null; recordingExtension?: string | null; notes?: string[] | null; recordingVoice?: "standard" | "clone" | null };
export type StoryDraft = {
  id: string; title: string; artistName: string; language: LanguageCode; status: "unpublished" | "published";
  reviewVersion: string; originalApproved: boolean; recordingExtension: string;
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
  return isLanguage(draft.language) && typeof draft.text === "string" && ["draft", "approved"].includes(String(draft.status))
    && (draft.recordingVoice == null || draft.recordingVoice === "standard" || draft.recordingVoice === "clone")
    && (draft.notes == null || (Array.isArray(draft.notes) && draft.notes.length <= 20 && draft.notes.every(note => typeof note === "string" && note.trim().length > 0 && note.length <= 1000)));
}

export async function readDraft(response: Response, id: string): Promise<StoryDraft> {
  if (!response.ok) {
    if (response.status === 404) throw new Error("This story could not be found. Check the editing link or upload a story first.");
    if (response.status === 409) throw new Error("This story changed. Reload the page and review the saved content before approving or uploading again. Your typed text is still here.");
    if (response.status === 413) throw new Error("The recording must be 25 MB or smaller.");
    if (response.status === 422) throw new Error("Save and approve the original first. AI drafts need a different supported language and at most 6,000 source characters.");
    if (response.status === 502 || response.status === 504) throw new Error("AI translation is unavailable. Your saved draft has not changed. Please try again later.");
    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      throw new Error(typeof problem?.detail === "string" ? problem.detail.slice(0, 300) : "Check the text and translation language, then try again.");
    }
    throw new Error("We couldn’t reach your saved story. Please try again. Any text you entered is still here.");
  }
  const draft = await response.json();
  if (!draft || typeof draft.id !== "string" || draft.id.toLowerCase() !== id.toLowerCase()
    || typeof draft.title !== "string" || typeof draft.artistName !== "string" || !isLanguage(draft.language)
    || !["unpublished", "published"].includes(draft.status) || typeof draft.reviewVersion !== "string"
    || typeof draft.originalApproved !== "boolean" || typeof draft.recordingExtension !== "string"
    || (draft.transcript !== null && typeof draft.transcript !== "string")
    || !Array.isArray(draft.translations) || !draft.translations.every(isTranslation)) {
    throw new Error("The API returned an unexpected story. Your edits have not been confirmed saved.");
  }
  return draft;
}

export async function approveStory(id: string, language: LanguageCode, reviewVersion: string, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/approvals/${language}`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewVersion }), signal,
  }), id);
}

export async function uploadTranslationRecording(id: string, language: LanguageCode, form: FormData, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/recordings/${language}`, { method: "POST", body: form, signal }), id);
}

export async function getStoryDraft(id: string, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/drafts`, { cache: "no-store", signal }), id);
}

export async function saveStoryText(id: string, section: "transcript" | LanguageCode, text: string, signal?: AbortSignal, notes?: string[]): Promise<StoryDraft> {
  const invalid = storyTextError(text);
  if (invalid) throw new Error(invalid);
  const suffix = section === "transcript" ? "transcript" : `translations/${encodeURIComponent(section)}`;
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/drafts/${suffix}`, {
    method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ text, ...(section === "transcript" ? {} : { notes }) }), signal,
  }), id);
}

export async function draftTranslation(id: string, language: LanguageCode, reviewVersion: string, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/drafts/translations/${language}/ai-draft`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewVersion }), signal,
  }), id);
}

export async function generateVoice(id: string, language: LanguageCode, reviewVersion: string, signal?: AbortSignal): Promise<StoryDraft> {
  const response = await fetch(`/api/stories/${encodeURIComponent(id)}/drafts/translations/${language}/ai-voice`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewVersion }), signal,
  });
  if (response.status === 422) throw new Error("Save the translation text first. AI voices support up to 1,800 characters (about two minutes); upload your own recording for longer text.");
  if (response.status === 502 || response.status === 504) throw new Error("AI voice is unavailable. Your saved draft has not changed. Please try again later.");
  return readDraft(response, id);
}
