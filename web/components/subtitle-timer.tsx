"use client";

import { useRef, useState } from "react";
import type { LanguageCode } from "@/lib/languages";
import type { StoryDraft } from "@/lib/story-drafts";
import { formatSeconds, parseSeconds, saveSubtitleTiming, subtitleLines, timingError, type SubtitleTiming } from "@/lib/subtitles";
import { RecordingPlayer } from "./recording-player";

type Props = {
  storyId: string; language: LanguageCode; languageName: string; original: boolean; text: string; timing: SubtitleTiming | null | undefined;
  reviewVersion: string; src: string; extension: string; recordingLabel: string; disabled: boolean;
  onSaved: (draft: StoryDraft, message: string) => void; onError: (message: string) => void;
};

// The artist plays the recording and presses "Mark next line" as each line begins, or types the seconds directly.
export function SubtitleTimer({ storyId, language, languageName, original, text, timing, reviewVersion, src, extension, recordingLabel, disabled, onSaved, onError }: Props) {
  const lines = subtitleLines(text);
  const savedFits = timing && timing.startsMs.length === lines.length;
  const [starts, setStarts] = useState<string[]>(() => lines.map((_, index) => savedFits ? formatSeconds(timing.startsMs[index]) : ""));
  const [end, setEnd] = useState(savedFits ? formatSeconds(timing.endMs) : "");
  const [busy, setBusy] = useState(false);
  const now = useRef(0);
  const media = useRef<HTMLMediaElement | null>(null);
  const prefix = `timing-${language}`;
  const next = starts.findIndex(value => !value.trim());

  function mark() {
    const value = formatSeconds(now.current);
    if (next >= 0) setStarts(current => current.map((item, index) => index === next ? value : item));
    else setEnd(value);
  }

  async function save() {
    const parsed = starts.map(parseSeconds);
    const endMs = parseSeconds(end);
    const invalid = timingError(parsed, endMs, lines.length);
    if (invalid) { onError(invalid); return; }
    setBusy(true);
    try {
      const draft = await saveSubtitleTiming(storyId, language, { startsMs: parsed as number[], endMs: endMs! }, reviewVersion);
      onSaved(draft, `${languageName} subtitle timing saved. Approve this language again to publish it.`);
    } catch (failure) { onError(failure instanceof Error ? failure.message : "Subtitle timing could not be saved."); }
    finally { setBusy(false); }
  }

  return <details className="subtitle-timer">
    <summary>Time {languageName} subtitles · {timing ? "Timed" : "Not timed"}</summary>
    <p className="draft-notice">Play the recording and press <strong>Mark next line</strong> as each line begins, then once more when the words end. You can also type the seconds. {original ? "Saving the original’s timing removes every approval for this story until you approve again." : "Saving timing removes this language’s approval until you approve it again."}</p>
    <RecordingPlayer key={src} src={src} extension={extension} label={recordingLabel} onTime={milliseconds => { now.current = milliseconds; }} mediaRef={element => { media.current = element; }} />
    <fieldset disabled={disabled || busy}>
      <legend className="sr-only">{languageName} subtitle times</legend>
      <button className="retry-button secondary-button" type="button" onClick={mark}>{next >= 0 ? `Mark next line (${next + 1} of ${lines.length})` : "Mark the end"}</button>
      <ol className="subtitle-lines">
        {lines.map((line, index) => <li key={`${prefix}-${index}`}>
          <label htmlFor={`${prefix}-${index}`}><span className="subtitle-line-text">{line}</span> Start of line {index + 1} (seconds)</label>
          <input id={`${prefix}-${index}`} inputMode="decimal" value={starts[index] ?? ""} onChange={event => setStarts(current => current.map((item, position) => position === index ? event.target.value : item))} />
        </li>)}
      </ol>
      <label htmlFor={`${prefix}-end`}>End of the last line (seconds)</label>
      <input id={`${prefix}-end`} inputMode="decimal" value={end} onChange={event => setEnd(event.target.value)} />
      <div className="subtitle-actions">
        <button className="retry-button" type="button" onClick={() => void save()}>Save {languageName} timing</button>
        <button className="text-control" type="button" onClick={() => { setStarts(lines.map(() => "")); setEnd(""); if (media.current) media.current.currentTime = 0; }}>Clear times</button>
      </div>
    </fieldset>
  </details>;
}
