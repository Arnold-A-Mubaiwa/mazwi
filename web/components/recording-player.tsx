"use client";

import { useState } from "react";

export function RecordingPlayer({ src, extension, label }: { src: string; extension: string; label: string }) {
  const [failed, setFailed] = useState(false);
  return <div className="recording-player">
    <p>{label}</p>
    {[".mp3", ".wav", ".m4a"].includes(extension)
      ? <audio controls preload="metadata" src={src} aria-label={label} onError={() => setFailed(true)} />
      : <video controls playsInline preload="metadata" src={src} aria-label={label} onError={() => setFailed(true)} />}
    {failed && <p className="upload-error">This recording could not be played. Check the file or try another browser-supported recording.</p>}
  </div>;
}
