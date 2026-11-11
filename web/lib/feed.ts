import { LANGUAGES, type LanguageCode } from "./languages.ts";
import { isCue, type SubtitleCue } from "./subtitles.ts";

export type ViewerStory = {
  id: string; title: string; artistName: string; community: string; country: string; type: string;
  originalLanguage: LanguageCode; textLanguage: LanguageCode; audioLanguage: LanguageCode;
  text: string; recordingExtension: string; originalRecordingExtension?: string; reviewVersion: string; availableLanguages: LanguageCode[]; mediaUrl: string; audioVoice?: "standard" | "clone" | null; speakerChecked?: boolean;
  cues?: SubtitleCue[] | null; subtitlesUrl?: string | null;
};
export type FeedPage = { items: ViewerStory[]; nextOffset: number | null };
export function languageName(code: string): string { return LANGUAGES.find(item => item.code === code)?.name ?? code; }
export function playbackLabel(story: ViewerStory): string {
  if (story.audioVoice && story.audioLanguage !== story.originalLanguage) return `AI voice, approved by ${story.artistName}`;
  return story.audioLanguage === story.originalLanguage ? `Original ${languageName(story.audioLanguage)} audio` : `Artist-provided ${languageName(story.audioLanguage)} recording`;
}
// Honest about who checked a translation: the artist may not speak the language they approved.
export function speakerNote(story: ViewerStory): string {
  const name = languageName(story.textLanguage);
  return story.speakerChecked === true ? `Approved by ${story.artistName}, who checked the ${name}.` : `Approved by ${story.artistName}. Not checked by ${/^[aeiou]/i.test(name) ? "an" : "a"} ${name} speaker.`;
}
export function viewingNote(story: ViewerStory, preferred: LanguageCode): string {
  if (story.textLanguage !== preferred) return `An approved ${languageName(preferred)} version isn’t available yet. Showing the approved ${languageName(story.textLanguage)} original.`;
  if (story.textLanguage === story.originalLanguage) return `Artist-approved ${languageName(story.textLanguage)} version.`;
  if (story.audioLanguage !== story.textLanguage) return `Approved ${languageName(story.textLanguage)} text with original ${languageName(story.audioLanguage)} audio. No translated recording is available. ${speakerNote(story)}`;
  return speakerNote(story);
}
function isStory(value: unknown): value is ViewerStory {
  if (!value || typeof value !== "object") return false;
  const s = value as Record<string, unknown>;
  const known = (code: unknown) => LANGUAGES.some(language => language.code === code);
  return ["id", "title", "artistName", "community", "country", "type", "text", "recordingExtension", "reviewVersion", "mediaUrl"].every(key => typeof s[key] === "string")
    && known(s.originalLanguage) && known(s.textLanguage) && known(s.audioLanguage)
    && Array.isArray(s.availableLanguages) && s.availableLanguages.every(known)
    && (s.speakerChecked == null || typeof s.speakerChecked === "boolean")
    && (s.cues == null || (Array.isArray(s.cues) && s.cues.every(isCue)))
    && (s.subtitlesUrl == null || String(s.subtitlesUrl).startsWith(`/api/feed/${s.id}/subtitles?`))
    && String(s.mediaUrl).startsWith(`/api/feed/${s.id}/recording?`);
}
async function request(url: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetch(url, { cache: "no-store", signal });
  if (response.status === 404) throw new Error("This story is not available. It may be unpublished or awaiting approval.");
  if (!response.ok) throw new Error("Stories could not be loaded. Please try again.");
  return response.json();
}
export async function getFeed(language: LanguageCode, offset = 0, signal?: AbortSignal): Promise<FeedPage> {
  const page = await request(`/api/feed?language=${language}&offset=${offset}&limit=20`, signal) as FeedPage;
  if (!page || !Array.isArray(page.items) || !page.items.every(isStory) || (page.nextOffset !== null && (!Number.isInteger(page.nextOffset) || page.nextOffset <= offset))) throw new Error("The API returned unexpected stories.");
  return page;
}
export async function getViewerStory(id: string, language: LanguageCode, signal?: AbortSignal): Promise<ViewerStory> {
  const story = await request(`/api/feed/${encodeURIComponent(id)}?language=${language}`, signal);
  if (!isStory(story) || story.id.toLowerCase() !== id.toLowerCase()) throw new Error("The API returned an unexpected story.");
  return story;
}
