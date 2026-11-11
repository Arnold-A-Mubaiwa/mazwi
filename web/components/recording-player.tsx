"use client";

import { useEffect, useRef, useState } from "react";
import { cueAt, type SubtitleCue } from "@/lib/subtitles";
import { createPlayTracker } from "@/lib/plays";

type Props = {
  src: string; extension: string; label: string;
  // Timed lines for exactly this recording; shown under the player as it plays.
  cues?: SubtitleCue[] | null; subtitlesUrl?: string | null; subtitleLanguage?: string;
  onTime?: (milliseconds: number) => void; mediaRef?: (element: HTMLMediaElement | null) => void;
  // Called once after three seconds of real listening.
  onPlayed?: () => void;
};

export function RecordingPlayer({ src, extension, label, cues, subtitlesUrl, subtitleLanguage, onTime, mediaRef, onPlayed }: Props) {
  const [failed, setFailed] = useState(false);
  const [now, setNow] = useState(0);
  const player = useRef<HTMLMediaElement | null>(null);
  const played = useRef(onPlayed);
  useEffect(() => { played.current = onPlayed; }, [onPlayed]);
  const tracker = useRef<ReturnType<typeof createPlayTracker> | null>(null);
  const track = (milliseconds: number, playing: boolean) => {
    tracker.current ??= createPlayTracker(() => played.current?.());
    tracker.current.update(milliseconds, playing);
  };
  const finish = () => {
    tracker.current ??= createPlayTracker(() => played.current?.());
    tracker.current.finish();
  };
  useEffect(() => {
    const element = player.current;
    return () => { element?.pause(); element?.removeAttribute("src"); element?.load(); };
  }, []);
  const attach = (element: HTMLMediaElement | null) => { player.current = element; mediaRef?.(element); };
  const update = (event: React.SyntheticEvent<HTMLMediaElement>) => {
    const milliseconds = Math.round(event.currentTarget.currentTime * 1000);
    setNow(milliseconds);
    onTime?.(milliseconds);
    track(milliseconds, !event.currentTarget.paused);
  };
  const cue = cueAt(cues, now);
  return <div className="recording-player">
    <p>{label}</p>
    {[".mp3", ".wav", ".m4a"].includes(extension)
      ? <audio ref={attach} controls preload="metadata" src={src} aria-label={label} onError={() => setFailed(true)} onTimeUpdate={update} onSeeked={update} onEnded={finish} />
      : <video ref={attach} controls playsInline preload="metadata" src={src} aria-label={label} onError={() => setFailed(true)} onTimeUpdate={update} onSeeked={update} onEnded={finish}>
        {subtitlesUrl && <track kind="subtitles" src={subtitlesUrl} srcLang={subtitleLanguage} label="Artist-timed subtitles" />}
      </video>}
    {cues && cues.length > 0 && <p className="subtitle-line" lang={subtitleLanguage} data-testid="subtitle-line">{cue?.text ?? " "}</p>}
    {failed && <p className="upload-error">This recording could not be played. Check the file or try another browser-supported recording.</p>}
  </div>;
}
