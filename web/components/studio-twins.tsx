"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { decideTwin, getTwins, twinLine, type StoryTwin } from "@/lib/twins";

/** Story Twins in Studio: AI suggests, the artist decides. Nothing is shown to viewers until an artist confirms. */
export function StudioTwins() {
  const [twins, setTwins] = useState<StoryTwin[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    getTwins(controller.signal).then(value => { setTwins(value); setError(null); }).catch(failure => {
      if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : "Story Twins could not be loaded.");
    });
    return () => controller.abort();
  }, [retry]);

  async function decide(twin: StoryTwin, decision: "confirm" | "reject") {
    setBusy(true); setMessage(""); setError(null);
    try {
      await decideTwin(twin.storyId, twin.twinStoryId, decision);
      setMessage(decision === "confirm"
        ? `Linked “${twin.storyTitle}” with “${twin.twinTitle}”. Viewers can now find one from the other.`
        : twin.status === "confirmed" ? `Unlinked “${twin.twinTitle}”. Mazwi will not suggest it again.` : `Marked “${twin.twinTitle}” as not the same story. Mazwi will not suggest it again.`);
      setRetry(value => value + 1);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "That did not work.");
    } finally { setBusy(false); }
  }

  const suggested = twins?.filter(twin => twin.status === "suggested") ?? [];
  const confirmed = twins?.filter(twin => twin.status === "confirmed") ?? [];
  return <section className="studio-twins" aria-labelledby="twins-heading">
    <p className="eyebrow">STORY TWINS</p>
    <h2 id="twins-heading">The same story, another voice.</h2>
    <p>Mazwi looks for other artists telling the same story in another language. Listen, then decide. Nothing is shown to viewers until you or the other artist confirm it, and you can unlink at any time.</p>
    {message && <p role="status">{message}</p>}
    {error && <p role="alert">{error}</p>}
    {twins === null && !error && <p>Looking for twins…</p>}
    {twins !== null && twins.length === 0 && <p data-testid="no-twins">No twins yet. Mazwi checks each story after you approve it.</p>}
    {suggested.length > 0 && <><h3>Suggested</h3><ul className="twin-list">{suggested.map(twin => <li key={`${twin.storyId}-${twin.twinStoryId}`} className="twin-card" data-testid="twin-suggestion">
      <p>Your story <strong>{twin.storyTitle}</strong> may be the same tale as <strong>{twin.twinTitle}</strong>.</p>
      <p>{twinLine(twin)}. <Link className="text-link" href={`/story/${twin.twinStoryId}`}>Listen first ↗</Link></p>
      <button className="retry-button" disabled={busy} onClick={() => void decide(twin, "confirm")}>Yes, same story</button>
      <button className="retry-button secondary-button" disabled={busy} onClick={() => void decide(twin, "reject")}>Not the same</button>
    </li>)}</ul></>}
    {confirmed.length > 0 && <><h3>Linked</h3><ul className="twin-list">{confirmed.map(twin => <li key={`${twin.storyId}-${twin.twinStoryId}`} className="twin-card" data-testid="twin-confirmed">
      <p><strong>{twin.storyTitle}</strong> is linked with <strong>{twin.twinTitle}</strong>.</p>
      <p>{twinLine(twin)}. <Link className="text-link" href={`/story/${twin.twinStoryId}`}>Watch ↗</Link></p>
      <button className="retry-button secondary-button" disabled={busy} onClick={() => void decide(twin, "reject")}>Unlink</button>
    </li>)}</ul></>}
  </section>;
}
