"use client";

import { useEffect, useRef, useState } from "react";
import { resyncTarget } from "@/lib/dubbing";
import { cueAt, type SubtitleCue } from "@/lib/subtitles";

type Props = { videoSrc: string; voiceSrc: string; label: string; cues?: SubtitleCue[] | null; subtitlesUrl?: string | null; language: string };

// Plays the original video with the translated voice. The video's own controls drive both:
// play, pause, seek, speed, volume and mute are mirrored onto the voice; the video's original sound is silenced.
export function DubbedPlayer({ videoSrc, voiceSrc, label, cues, subtitlesUrl, language }: Props) {
  const video = useRef<HTMLVideoElement | null>(null);
  const voice = useRef<HTMLAudioElement | null>(null);
  const [now, setNow] = useState(0);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const picture = video.current;
    const sound = voice.current;
    if (!picture || !sound) return;
    // Route the video's soundtrack into an audio graph that goes nowhere, so its volume controls stay usable for the voice.
    let context: AudioContext | null = null;
    try {
      context = new AudioContext();
      context.createMediaElementSource(picture);
      picture.dataset.originalSound = "routed";
    } catch {
      picture.muted = true;
      picture.dataset.originalSound = "muted";
    }

    const resync = () => {
      const target = resyncTarget(picture.currentTime, sound.currentTime, sound.duration);
      if (target !== null) sound.currentTime = target;
    };
    const play = () => { resync(); if (picture.currentTime < (Number.isFinite(sound.duration) ? sound.duration : Infinity)) void sound.play().catch(() => {}); };
    // When the video ends first, let a longer voice finish its last words.
    const pause = () => { if (!picture.ended) sound.pause(); };
    const seek = () => { sound.currentTime = Math.min(picture.currentTime, Number.isFinite(sound.duration) ? sound.duration : picture.currentTime); };
    const rate = () => { sound.playbackRate = picture.playbackRate; };
    const volume = () => { sound.volume = picture.volume; if (context) sound.muted = picture.muted; };
    const tick = () => { if (!picture.paused) resync(); };
    const events: [string, () => void][] = [["play", play], ["pause", pause], ["seeking", seek], ["seeked", seek], ["ratechange", rate], ["volumechange", volume], ["timeupdate", tick], ["waiting", pause], ["playing", play]];
    for (const [name, handler] of events) picture.addEventListener(name, handler);
    return () => {
      for (const [name, handler] of events) picture.removeEventListener(name, handler);
      sound.pause();
      void context?.close().catch(() => {});
    };
  }, []);

  const cue = cueAt(cues, now);
  return <div className="recording-player dubbed-player">
    <p>{label}</p>
    <video ref={video} controls playsInline preload="metadata" src={videoSrc} aria-label={label} onError={() => setFailed(true)}>
      {subtitlesUrl && <track kind="subtitles" src={subtitlesUrl} srcLang={language} label="Artist-timed subtitles" />}
    </video>
    <audio ref={voice} preload="auto" src={voiceSrc} onError={() => setFailed(true)} data-testid="dubbed-voice"
      onTimeUpdate={event => setNow(Math.round(event.currentTarget.currentTime * 1000))} />
    {cues && cues.length > 0 && <p className="subtitle-line" lang={language} data-testid="subtitle-line">{cue?.text ?? " "}</p>}
    {failed && <p className="upload-error">This dubbed story could not be played. Try “Watch with original sound”.</p>}
  </div>;
}
