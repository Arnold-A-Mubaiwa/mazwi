"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { LANGUAGES, type LanguageCode } from "@/lib/languages";
import { getStoryDraft, saveStoryText, MAX_STORY_TEXT, type StoryDraft } from "@/lib/story-drafts";

export function StoryDraftEditor({ id }: { id: string }) {
  const [draft, setDraft] = useState<StoryDraft | null>(null);
  const [transcript, setTranscript] = useState("");
  const [translations, setTranslations] = useState<Partial<Record<LanguageCode, string>>>({});
  const [target, setTarget] = useState<LanguageCode>("xh");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const saving = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    getStoryDraft(id, controller.signal).then(story => {
      setDraft(story);
      setTranscript(story.transcript ?? "");
      setTranslations(Object.fromEntries(story.translations.map(translation => [translation.language, translation.text])));
      setTarget(LANGUAGES.find(language => language.code !== story.language)!.code);
    }).catch(failure => {
      if (!controller.signal.aborted) setLoadError(failure instanceof Error ? failure.message : "Could not load this story.");
    });
    return () => controller.abort();
  }, [id, attempt]);
  useEffect(() => () => saving.current?.abort(), []);

  const unsaved = draft !== null && (transcript !== (draft.transcript ?? "")
    || Object.entries(translations).some(([language, text]) => text !== (draft.translations.find(item => item.language === language)?.text ?? "")));
  useEffect(() => {
    if (!unsaved) return;
    const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); };
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [unsaved]);

  async function save(section: "transcript" | LanguageCode) {
    setBusy(true); setSaveError(null); setMessage("");
    saving.current = new AbortController();
    try {
      const updated = await saveStoryText(id, section, section === "transcript" ? transcript : (translations[section] ?? ""), saving.current.signal);
      setDraft(updated);
      if (section === "transcript") {
        setTranscript(updated.transcript ?? "");
        setMessage("Original transcript saved. Recheck existing translations if you changed its meaning.");
      } else {
        setTranslations(current => ({ ...current, [section]: updated.translations.find(item => item.language === section)?.text ?? "" }));
        setMessage(`${LANGUAGES.find(language => language.code === section)?.name} translation saved as a draft. It is not approved or published.`);
      }
    } catch (failure) {
      if (!saving.current.signal.aborted) setSaveError(failure instanceof Error ? failure.message : "Could not save. Your text is still here.");
    } finally { setBusy(false); }
  }

  if (!draft) return <section className="draft-loading" aria-live="polite">
    {loadError ? <><p role="alert">{loadError}</p><button className="retry-button" onClick={() => { setLoadError(null); setAttempt(value => value + 1); }}>Try loading again</button><p><Link className="text-link" href="/upload">Upload a story</Link></p></> : <p role="status">Loading your saved story…</p>}
  </section>;

  const originalLanguage = LANGUAGES.find(language => language.code === draft.language)?.name;
  const translationReady = Boolean(draft.transcript) && transcript === draft.transcript;
  const targetText = translations[target] ?? "";
  const targetSaved = draft.translations.find(item => item.language === target);

  return <>
    <div className="draft-story-heading"><div><h2>{draft.title}</h2><p>By {draft.artistName} · Original language: {originalLanguage}</p></div><span className="draft-badge">Unpublished</span></div>
    <p className="draft-return-note">Bookmark this editing link to come back to your saved work. Keep a copy of any unsaved text before leaving the page.</p>
    <div className="draft-grid">
      <section className="draft-card" aria-labelledby="original-heading">
        <p className="eyebrow">01 / THE ORIGINAL WORDS</p><h2 id="original-heading">Start in {originalLanguage}.</h2>
        <p>Listen to your recording in your own player, then type the words here. Keep names, proverbs and line breaks.</p>
        <form onSubmit={event => { event.preventDefault(); void save("transcript"); }}>
          <fieldset disabled={busy}><legend className="sr-only">Original transcript</legend>
            <label htmlFor="original-transcript">Original transcript</label>
            <textarea id="original-transcript" value={transcript} onChange={event => setTranscript(event.target.value)} maxLength={MAX_STORY_TEXT} required rows={12} aria-describedby="original-count" />
            <p className="draft-count" id="original-count">{transcript.length.toLocaleString()} / 20,000 characters · {transcript === draft.transcript ? "Saved" : "Not saved"}</p>
            <button className="retry-button" type="submit">Save original transcript</button>
          </fieldset>
        </form>
      </section>
      <section className="draft-card" aria-labelledby="translation-heading">
        <p className="eyebrow">02 / ANOTHER LANGUAGE</p><h2 id="translation-heading">Carry the meaning.</h2>
        <p>Write your own translation or paste one you have permission to use. Nothing here is generated by AI.</p>
        {!translationReady && <p className="draft-notice">Save the original transcript first, including any changes, to edit translations.</p>}
        <form onSubmit={event => { event.preventDefault(); void save(target); }}>
          <fieldset disabled={busy || !translationReady}><legend className="sr-only">Translation draft</legend>
            <label htmlFor="translation-language">Translation language</label>
            <select id="translation-language" value={target} onChange={event => { setTarget(event.target.value as LanguageCode); setMessage(""); }}>
              {LANGUAGES.filter(language => language.code !== draft.language).map(language => <option key={language.code} value={language.code}>{language.name}</option>)}
            </select>
            <label htmlFor="translation-text">Translation text</label>
            <textarea id="translation-text" value={targetText} onChange={event => setTranslations(current => ({ ...current, [target]: event.target.value }))} maxLength={MAX_STORY_TEXT} required rows={9} aria-describedby="translation-count" />
            <p className="draft-count" id="translation-count">{targetText.length.toLocaleString()} / 20,000 characters · {targetSaved && targetSaved.text === targetText ? "Saved draft" : "Not saved"}</p>
            <button className="retry-button" type="submit">Save translation draft</button>
          </fieldset>
        </form>
      </section>
    </div>
    {busy && <p className="draft-feedback" role="status">Saving your words…</p>}
    {message && <p className="draft-feedback" role="status">{message}</p>}
    {saveError && <p className="upload-error" role="alert">{saveError}</p>}
    <p className="draft-privacy-note">Manual drafts only. No AI processing, voice generation, approval or publication has run. This local demo has no accounts; keep it on your own computer.</p>
  </>;
}
