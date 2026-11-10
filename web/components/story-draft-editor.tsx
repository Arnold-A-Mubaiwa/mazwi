"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { LANGUAGES, type LanguageCode } from "@/lib/languages";
import { getStoryDraft, saveStoryText, approveStory, uploadTranslationRecording, draftTranslation, draftTranscript, generateVoice, MAX_STORY_TEXT, MAX_VOICE_TEXT, type StoryDraft } from "@/lib/story-drafts";
import { FEATURES } from "@/lib/config";
import { recordingError } from "@/lib/stories";
import { RecordingPlayer } from "./recording-player";

export function StoryDraftEditor({ id }: { id: string }) {
  const [draft, setDraft] = useState<StoryDraft | null>(null);
  const [transcript, setTranscript] = useState("");
  const [translations, setTranslations] = useState<Partial<Record<LanguageCode, string>>>({});
  const [notes, setNotes] = useState<Partial<Record<LanguageCode, string[]>>>({});
  const [aiPending, setAiPending] = useState<"transcript" | "translation" | "voice" | null>(null);
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
      setNotes(Object.fromEntries(story.translations.map(translation => [translation.language, translation.notes ?? []])));
      setTarget(LANGUAGES.find(language => language.code !== story.language)!.code);
    }).catch(failure => {
      if (!controller.signal.aborted) setLoadError(failure instanceof Error ? failure.message : "Could not load this story.");
    });
    return () => controller.abort();
  }, [id, attempt]);
  useEffect(() => () => saving.current?.abort(), []);

  const unsaved = draft !== null && (transcript !== (draft.transcript ?? "")
    || Object.entries(translations).some(([language, text]) => text !== (draft.translations.find(item => item.language === language)?.text ?? ""))
    || Object.entries(notes).some(([language, values]) => JSON.stringify(values) !== JSON.stringify(draft.translations.find(item => item.language === language)?.notes ?? [])));
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
      const updated = await saveStoryText(id, section, section === "transcript" ? transcript : (translations[section] ?? ""), saving.current.signal, section === "transcript" ? undefined : (notes[section] ?? []));
      setDraft(updated);
      if (section === "transcript") {
        setTranscript(updated.transcript ?? "");
        setMessage("Original transcript saved. Recheck existing translations if you changed its meaning.");
      } else {
        setTranslations(current => ({ ...current, [section]: updated.translations.find(item => item.language === section)?.text ?? "" }));
        setNotes(current => ({ ...current, [section]: updated.translations.find(item => item.language === section)?.notes ?? [] }));
        setMessage(updated.translations.find(item => item.language === section)?.status === "approved"
          ? "Unchanged translation saved. Its existing approval is kept."
          : `${LANGUAGES.find(language => language.code === section)?.name} translation saved as a draft. It is not approved or published.`);
      }
    } catch (failure) {
      if (!saving.current.signal.aborted) setSaveError(failure instanceof Error ? failure.message : "Could not save. Your text is still here.");
    } finally { setBusy(false); }
  }

  async function generateDraft() {
    if (!draft || busy || unsaved || !draft.originalApproved) return;
    const language = target;
    setBusy(true); setAiPending("translation"); setSaveError(null); setMessage("");
    saving.current = new AbortController();
    try {
      const updated = await draftTranslation(id, language, draft.reviewVersion, saving.current.signal);
      const generated = updated.translations.find(item => item.language === language);
      setDraft(updated);
      setTranslations(current => ({ ...current, [language]: generated?.text ?? "" }));
      setNotes(current => ({ ...current, [language]: generated?.notes ?? [] }));
      setMessage("AI draft saved. Check and correct the language, names and meaning before approving. Nothing was approved automatically.");
    } catch (failure) {
      if (!saving.current.signal.aborted) setSaveError(failure instanceof Error ? failure.message : "AI translation failed. Your text is still here.");
    } finally { setBusy(false); setAiPending(null); }
  }

  async function generateTranscript() {
    if (!draft || busy || unsaved) return;
    if (draft.transcript && !window.confirm("Replace your saved transcript with an AI draft? This removes every approval for this story.")) return;
    setBusy(true); setAiPending("transcript"); setSaveError(null); setMessage("");
    saving.current = new AbortController();
    try {
      const updated = await draftTranscript(id, draft.reviewVersion, saving.current.signal);
      setDraft(updated);
      setTranscript(updated.transcript ?? "");
      setMessage("AI transcript saved as a draft. Check and correct this before approving. Nothing was approved automatically.");
    } catch (failure) {
      if (!saving.current.signal.aborted) setSaveError(failure instanceof Error ? failure.message : "AI transcription failed. Your saved transcript has not changed.");
    } finally { setBusy(false); setAiPending(null); }
  }

  async function generateTranslatedVoice() {
    if (!draft || busy || unsaved) return;
    const language = target;
    setBusy(true); setAiPending("voice"); setSaveError(null); setMessage("");
    saving.current = new AbortController();
    try {
      setDraft(await generateVoice(id, language, draft.reviewVersion, saving.current.signal));
      setMessage("AI voice saved as this language's recording. Listen to all of it and check pronunciation before approving. Nothing was approved automatically.");
    } catch (failure) {
      if (!saving.current.signal.aborted) setSaveError(failure instanceof Error ? failure.message : "AI voice failed. Your saved draft has not changed.");
    } finally { setBusy(false); setAiPending(null); }
  }

  async function approve(language: LanguageCode) {
    if (!draft) return;
    setBusy(true); setSaveError(null); setMessage("");
    saving.current = new AbortController();
    try {
      setDraft(await approveStory(id, language, draft.reviewVersion, saving.current.signal));
      setMessage(`${LANGUAGES.find(item => item.code === language)?.name} version approved. Editing its content will require approval again.`);
    } catch (failure) { setSaveError(failure instanceof Error ? failure.message : "Approval failed. Please try again."); }
    finally { setBusy(false); }
  }

  async function uploadRecording(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!draft) return;
    const form = new FormData(event.currentTarget);
    const file = form.get("recording");
    const invalid = recordingError(file instanceof File ? file : null);
    if (invalid) { setSaveError(invalid); return; }
    form.set("reviewVersion", draft.reviewVersion);
    setBusy(true); setSaveError(null); setMessage("");
    saving.current = new AbortController();
    try {
      setDraft(await uploadTranslationRecording(id, target, form, saving.current.signal));
      setMessage("Translated recording saved as a draft. Listen to it, then approve this language again.");
    } catch (failure) { setSaveError(failure instanceof Error ? failure.message : "Recording upload failed."); }
    finally { setBusy(false); }
  }

  if (!draft) return <section className="draft-loading" aria-live="polite">
    {loadError ? <><p role="alert">{loadError}</p><button className="retry-button" onClick={() => { setLoadError(null); setAttempt(value => value + 1); }}>Try loading again</button><p><Link className="text-link" href="/upload">Upload a story</Link></p></> : <p role="status">Loading your saved story…</p>}
  </section>;

  const originalLanguage = LANGUAGES.find(language => language.code === draft.language)?.name;
  const translationReady = Boolean(draft.transcript) && transcript === draft.transcript;
  const targetText = translations[target] ?? "";
  const targetSaved = draft.translations.find(item => item.language === target);

  return <>
    <div className="draft-story-heading"><div><h2>{draft.title}</h2><p>By {draft.artistName} · Original language: {originalLanguage}</p></div><span className="draft-badge">{draft.originalApproved ? "Original approved" : "Unpublished"}</span></div>
    <p className="draft-return-note"><Link className="text-link" href="/studio">Back to your studio ↗</Link> · Keep a copy of any unsaved text before leaving the page.</p>
    <div className="draft-grid">
      <section className="draft-card" aria-labelledby="original-heading">
        <p className="eyebrow">01 / THE ORIGINAL WORDS</p><h2 id="original-heading">Start in {originalLanguage}.</h2>
        <p>Listen to the original and check the words. Keep names, proverbs and line breaks.</p>
        <RecordingPlayer key={`original-${draft.reviewVersion}`} extension={draft.recordingExtension} label={`Original ${originalLanguage} recording`} src={`/api/stories/${id}/recordings/${draft.language}?v=${draft.reviewVersion}`} />
        {FEATURES.aiTranscript && <>
          <button className="retry-button secondary-button" type="button" disabled={busy || unsaved} onClick={() => void generateTranscript()}>AI first draft</button>
          <p className="draft-notice"><strong>Check and correct this before approving.</strong> AI transcripts of Shona and other African languages are often wrong. Listen to the recording and fix every line. Each request incurs an API charge.</p>
        </>}
        <form onSubmit={event => { event.preventDefault(); void save("transcript"); }}>
          <fieldset disabled={busy}><legend className="sr-only">Original transcript</legend>
            <label htmlFor="original-transcript">Original transcript</label>
            <textarea id="original-transcript" value={transcript} onChange={event => setTranscript(event.target.value)} maxLength={MAX_STORY_TEXT} required rows={12} aria-describedby="original-count" />
            <p className="draft-count" id="original-count">{transcript.length.toLocaleString()} / 20,000 characters · {transcript === draft.transcript ? "Saved" : "Not saved"}</p>
            <button className="retry-button" type="submit">Save original transcript</button>
          </fieldset>
        </form>
        <button className="retry-button secondary-button" disabled={busy || unsaved || !draft.transcript || draft.originalApproved} onClick={() => void approve(draft.language)}>{draft.originalApproved ? "Original is approved" : "Approve original"}</button>
      </section>
      <section className="draft-card" aria-labelledby="translation-heading">
        <p className="eyebrow">02 / ANOTHER LANGUAGE</p><h2 id="translation-heading">Carry the meaning.</h2>
        <p>{FEATURES.aiTranslation ? "Write your own translation or request an AI draft from your approved original. Always check the language, names and meaning yourself." : "Write your own translation or paste one you have permission to use."}</p>
        {!translationReady && <p className="draft-notice">Save the original transcript first, including any changes, to edit translations.</p>}
        <form onSubmit={event => { event.preventDefault(); void save(target); }}>
          <fieldset disabled={busy || !translationReady}><legend className="sr-only">Translation draft</legend>
            <label htmlFor="translation-language">Translation language</label>
            <select id="translation-language" value={target} onChange={event => { setTarget(event.target.value as LanguageCode); setMessage(""); }}>
              {LANGUAGES.filter(language => language.code !== draft.language).map(language => <option key={language.code} value={language.code}>{language.name}</option>)}
            </select>
            {FEATURES.aiTranslation && <>
              <button className="retry-button secondary-button" type="button" disabled={busy || unsaved || !draft.originalApproved || transcript.length > 6000} onClick={() => void generateDraft()}>AI draft</button>
              <p className="draft-notice">Save and approve the original first. AI drafts support up to 6,000 source characters and incur an API charge. Save your edits before generating a replacement.</p>
            </>}
            <label htmlFor="translation-text">Translation text</label>
            <textarea id="translation-text" value={targetText} onChange={event => setTranslations(current => ({ ...current, [target]: event.target.value }))} maxLength={MAX_STORY_TEXT} required rows={9} aria-describedby="translation-count" />
            <p className="draft-count" id="translation-count">{targetText.length.toLocaleString()} / 20,000 characters · {targetSaved && targetSaved.text === targetText ? "Saved draft" : "Not saved"}</p>
            {(notes[target] ?? []).length > 0 && <div className="translation-notes"><h3>Translation notes</h3>
              {(notes[target] ?? []).map((note, index) => <div key={`${target}-${index}`}>
                <label htmlFor={`translation-note-${index}`}>Translation note {index + 1}</label>
                <textarea id={`translation-note-${index}`} value={note} maxLength={1000} required rows={2} onChange={event => setNotes(current => ({ ...current, [target]: (current[target] ?? []).map((value, position) => position === index ? event.target.value : value) }))} />
                <button className="retry-button secondary-button" type="button" onClick={() => setNotes(current => ({ ...current, [target]: (current[target] ?? []).filter((_, position) => position !== index) }))}>Dismiss note {index + 1}</button>
              </div>)}
              <p>Save the translation draft to keep note edits or dismissals.</p>
            </div>}
            <button className="retry-button" type="submit">Save translation draft</button>
          </fieldset>
        </form>
        {targetSaved && <div className="translated-recording">
          <p className="draft-notice">{targetSaved.status === "approved" ? "This language is approved." : "This language is a draft."} Without a translated recording, viewers will hear clearly labelled original audio with approved translated text.</p>
          {targetSaved.recordingId && <RecordingPlayer key={targetSaved.recordingId} extension={targetSaved.recordingExtension ?? ".mp4"} label={targetSaved.recordingVoice ? "AI voice translated recording" : "Artist-provided translated recording"} src={`/api/stories/${id}/recordings/${target}?v=${draft.reviewVersion}`} />}
          {FEATURES.aiVoice && <>
            <button className="retry-button secondary-button" type="button" disabled={busy || unsaved || targetSaved.text.trim().length === 0 || targetSaved.text.length > MAX_VOICE_TEXT} onClick={() => void generateTranslatedVoice()}>Generate voice</button>
            <p className="draft-notice">Creates a standard AI voice (not your voice) from the saved translation, up to 1,800 characters (about two minutes). It replaces this language’s recording, needs approval again and incurs an API charge.</p>
          </>}
          <form onSubmit={uploadRecording}><fieldset disabled={busy || unsaved}>
            <legend>Optional translated recording</legend>
            <label htmlFor="translated-recording">Translated recording</label><input id="translated-recording" type="file" name="recording" accept=".mp3,.wav,.m4a,.mp4,.webm" required />
            <label className="permission-field"><input type="checkbox" name="permission" value="true" required /> I own this translated recording or have permission to upload and share it.</label>
            <button className="retry-button" type="submit">Save translated recording</button>
          </fieldset></form>
          <button className="retry-button secondary-button" disabled={busy || unsaved || !draft.originalApproved || targetSaved.status === "approved"} onClick={() => void approve(target)}>{targetSaved.status === "approved" ? "Translation is approved" : "Approve translation"}</button>
        </div>}
      </section>
    </div>
    {busy && <p className="draft-feedback" role="status">{aiPending === "voice" ? "Generating an AI voice…" : aiPending === "transcript" ? "Transcribing the original recording…" : aiPending ? "Creating an AI translation draft…" : "Saving your words…"}</p>}
    {message && <p className="draft-feedback" role="status">{message}</p>}
    {saveError && <p className="upload-error" role="alert">{saveError}</p>}
    <p className="draft-privacy-note">Nothing is approved automatically. Save and review every change before approval. AI transcription sends your original recording, AI drafting your saved transcript, and AI voice your saved translation to OpenAI only when you request it. This local demo has no accounts; keep it on your own computer.</p>
  </>;
}
