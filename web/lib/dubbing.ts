import { languageName, type ViewerStory } from "./feed.ts";

export const VIDEO_EXTENSIONS = [".mp4", ".webm"];
export const AUDIO_EXTENSIONS = [".mp3", ".wav", ".m4a"];
// How far the dubbed voice may drift from the video, in seconds, before it is moved back.
export const DRIFT_TOLERANCE = 0.3;

// Dub when the original is a video and the approved translation has its own audio-only recording.
export function canDub(story: ViewerStory): boolean {
  return VIDEO_EXTENSIONS.includes(story.originalRecordingExtension ?? "") && story.audioLanguage !== story.originalLanguage
    && story.audioLanguage === story.textLanguage && AUDIO_EXTENSIONS.includes(story.recordingExtension);
}

export function dubbedLabel(story: ViewerStory): string {
  const voice = story.audioVoice ? "AI voice" : `artist-provided ${languageName(story.audioLanguage)} recording`;
  return `Dubbed: ${voice}, approved by ${story.artistName}. The original video plays without its sound.`;
}

// The video is the clock. Returns where the voice should jump to, or null when it is close enough
// or the video is past the end of the voice (a shorter voice simply stops).
export function resyncTarget(videoTime: number, voiceTime: number, voiceDuration: number, tolerance = DRIFT_TOLERANCE): number | null {
  if (!Number.isFinite(videoTime) || videoTime < 0) return null;
  if (Number.isFinite(voiceDuration) && videoTime >= voiceDuration) return null;
  return Math.abs(videoTime - voiceTime) > tolerance ? videoTime : null;
}
