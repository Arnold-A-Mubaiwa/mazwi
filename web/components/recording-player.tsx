"use client";

import { useEffect, useRef, useState } from "react";

export function RecordingPlayer({ src, extension, label }: { src: string; extension: string; label: string }) {
  const [failed, setFailed] = useState(false);
  const player = useRef<HTMLMediaElement | null>(null);
  useEffect(() => {
    const element = player.current;
    return () => { element?.pause(); element?.removeAttribute("src"); element?.load(); };
  }, []);
  return <div className="recording-player">
    <p>{label}</p>
    {[".mp3", ".wav", ".m4a"].includes(extension)
      ? <audio ref={element => { player.current = element; }} controls preload="metadata" src={src} aria-label={label} onError={() => setFailed(true)} />
      : <video ref={element => { player.current = element; }} controls playsInline preload="metadata" src={src} aria-label={label} onError={() => setFailed(true)} />}
    {failed && <p className="upload-error">This recording could not be played. Check the file or try another browser-supported recording.</p>}
  </div>;
}
