"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { FEATURES } from "@/lib/config";
import { LANGUAGES } from "@/lib/languages";
import { recordingError, uploadStory, type UploadedStory } from "@/lib/stories";

export function UploadForm() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [story, setStory] = useState<UploadedStory | null>(null);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);

  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const recording = form.get("recording");
    const invalid = recordingError(recording instanceof File ? recording : null);
    if (invalid) { setError(invalid); return; }
    setError(null);
    setBusy(true);
    controller.current = new AbortController();
    try {
      setStory(await uploadStory(form, controller.current.signal));
    } catch (failure) {
      if (!controller.current.signal.aborted) setError(failure instanceof Error ? failure.message : "The upload failed. Please try again.");
    } finally { setBusy(false); }
  }

  if (story) return <section className="upload-confirmation" role="status" aria-live="polite">
    <p className="eyebrow">SAVED · UNPUBLISHED</p>
    <h2>Your story has a home.</h2>
    <p><strong>{story.title}</strong> by {story.artistName} has been saved on this computer.</p>
    <p>It is not in the feed. You can write a transcript and manual translations next. Artist approval and playback are coming later. No AI processing has run.</p>
    <p className="upload-reference">Story reference: {story.id}</p>
    {FEATURES.manualEditing && <p><Link className="text-link" href={`/story/${story.id}/review`}>Write transcript and translations <span aria-hidden="true">↗</span></Link></p>}
    <button className="retry-button" onClick={() => setStory(null)}>Upload another story</button>
  </section>;

  return <form className="upload-form" onSubmit={submit}>
    <fieldset disabled={busy}>
      <legend className="sr-only">Story details and recording</legend>
      <label className="recording-field"><span id="recording-label">Your recording</span>
        <input type="file" name="recording" accept=".mp3,.wav,.m4a,.mp4,.webm" required aria-labelledby="recording-label" aria-describedby="recording-help" />
        <span id="recording-help">Audio or video · MP3, WAV, M4A, MP4 or WebM · Up to 25 MB</span>
      </label>
      <label>Story title<input name="title" maxLength={200} required autoComplete="off" placeholder="What is your story called?" /></label>
      <div className="upload-grid">
        <label>Artist name<input name="artistName" maxLength={100} required autoComplete="name" /></label>
        <label>Community<input name="community" maxLength={100} required placeholder="For example, Shona, Zimbabwe" /></label>
        <label><span id="country-label">Country code</span><input name="country" minLength={2} maxLength={2} pattern="[A-Za-z]{2}" required placeholder="ZW" aria-labelledby="country-label" aria-describedby="country-help" /><span id="country-help">Two letters, for example ZW or ZA.</span></label>
        <label><span id="type-label">Story type</span><select name="type" aria-labelledby="type-label" defaultValue="story"><option value="story">Story</option><option value="poem">Poem</option><option value="folktale">Folktale</option><option value="song">Song</option></select></label>
        <label><span id="language-label">Original language</span><select name="language" aria-labelledby="language-label" defaultValue="sn">{LANGUAGES.map(language => <option key={language.code} value={language.code}>{language.name}</option>)}</select></label>
      </div>
      <label className="permission-field"><input type="checkbox" name="permission" value="true" required /><span>I own this recording or have permission to upload it and allow Mazwi to store and process it. This does not allow voice cloning.</span></label>
      <button className="retry-button" type="submit">{busy ? "Saving your story…" : "Save my story"}</button>
    </fieldset>
    {busy && <p role="status" aria-live="polite">Uploading your recording. Please keep this page open.</p>}
    {error && <p className="upload-error" role="alert">{error}</p>}
  </form>;
}
