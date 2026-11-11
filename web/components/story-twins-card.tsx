"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { findLanguage, type LanguageCode } from "@/lib/languages";
import { getViewerTwins, requestVersion, type ViewerTwin } from "@/lib/twins";

/** "Same story, another voice": confirmed twins of the story being watched. */
export function StoryTwinsCard({ storyId, language }: { storyId: string; language: LanguageCode }) {
  const [twins, setTwins] = useState<ViewerTwin[]>([]);
  const [asked, setAsked] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    getViewerTwins(storyId, language, controller.signal).then(setTwins).catch(() => undefined);
    return () => controller.abort();
  }, [storyId, language]);
  if (!twins.length) return null;
  const yours = findLanguage(language)?.name ?? language;

  async function ask(twin: ViewerTwin) {
    setBusy(true);
    try {
      await requestVersion(twin.storyId, language);
      setAsked(current => ({ ...current, [twin.storyId]: `Thank you. ${twin.artistName} will see that listeners asked for a ${yours} version.` }));
    } catch (failure) {
      setAsked(current => ({ ...current, [twin.storyId]: failure instanceof Error ? failure.message : "Your request could not be sent." }));
    } finally { setBusy(false); }
  }

  return <section className="twin-viewer-card" aria-labelledby="twin-viewer-heading">
    <p className="eyebrow">SAME STORY, ANOTHER VOICE</p>
    <h2 id="twin-viewer-heading">Told by another artist</h2>
    <ul className="twin-list">{twins.map(twin => {
      const theirs = findLanguage(twin.language)?.name ?? twin.language;
      return <li key={twin.storyId} className="twin-card" data-testid="viewer-twin">
        <p>Also told in {theirs} by {twin.artistName} ({twin.community}): <strong>{twin.title}</strong></p>
        {twin.availableInYourLanguage
          ? <Link className="retry-button" href={`/story/${twin.storyId}`}>Hear it in {yours} ↗</Link>
          : <>
            <p className="viewer-note">{twin.artistName} has not approved a {yours} version yet. You can hear it in {theirs}, with its approved words.</p>
            <Link className="retry-button" href={`/story/${twin.storyId}`}>Hear it in {theirs} ↗</Link>
            {asked[twin.storyId]
              ? <p role="status">{asked[twin.storyId]}</p>
              : <button className="retry-button secondary-button" disabled={busy} onClick={() => void ask(twin)}>Ask for a {yours} version</button>}
          </>}
      </li>;
    })}</ul>
  </section>;
}
