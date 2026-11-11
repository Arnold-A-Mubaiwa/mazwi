"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { getReports, moderate, reasonLabel, REPORT_REASONS, reportStory, type ModerationAction, type ReportReason, type ReportedStory } from "@/lib/moderation";

export function ReportStory({ id }: { id: string }) {
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setBusy(true); setError("");
    try {
      await reportStory(id, String(form.get("reason") ?? "") as ReportReason, String(form.get("note") ?? ""));
      setOpen(false);
      setMessage("Thank you. Mazwi moderators will look at this story.");
    } catch (failure) { setError(failure instanceof Error ? failure.message : "Please try again."); }
    finally { setBusy(false); }
  }
  if (message) return <p className="share-message" role="status">{message}</p>;
  if (!open) return <button className="text-control report-toggle" type="button" onClick={() => setOpen(true)}>Report this story</button>;
  return <form className="report-form" onSubmit={submit}>
    <fieldset disabled={busy}><legend>Report this story</legend>
      <label htmlFor={`report-reason-${id}`}>Reason</label>
      <select id={`report-reason-${id}`} name="reason" defaultValue="" required>
        <option value="" disabled>Choose a reason</option>
        {REPORT_REASONS.map(reason => <option key={reason.value} value={reason.value}>{reason.label}</option>)}
      </select>
      <label htmlFor={`report-note-${id}`}>Anything moderators should know? (optional)</label>
      <textarea id={`report-note-${id}`} name="note" maxLength={500} rows={3} />
      <p className="draft-notice">Reports are anonymous: Mazwi does not record who sent them.</p>
      <div className="subtitle-actions"><button className="retry-button" type="submit">Send report</button><button className="text-control" type="button" onClick={() => setOpen(false)}>Cancel</button></div>
    </fieldset>
    {error && <p className="upload-error" role="alert">{error}</p>}
  </form>;
}

const ACTIONS: Record<ModerationAction, string> = { hide: "Hidden from the feed for review.", restore: "Back in the feed; reports closed.", resolve: "Reports closed." };

export function ModerationQueue() {
  const [items, setItems] = useState<ReportedStory[] | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [refresh, setRefresh] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    getReports(controller.signal).then(setItems).catch(failure => { if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : "Reports could not be loaded."); });
    return () => controller.abort();
  }, [refresh]);
  async function act(item: ReportedStory, action: ModerationAction) {
    try { await moderate(item.storyId, action); setMessage(`“${item.title}”: ${ACTIONS[action]}`); setRefresh(value => value + 1); }
    catch (failure) { setError(failure instanceof Error ? failure.message : "That did not work."); }
  }
  if (error && !items) return <p className="upload-error" role="alert">{error}</p>;
  if (!items) return <p role="status">Loading reports…</p>;
  return <>
    {message && <p className="draft-feedback" role="status">{message}</p>}
    {error && <p className="upload-error" role="alert">{error}</p>}
    {!items.length && <div className="empty-stories"><h2>No open reports.</h2><p>Reported stories appear here, newest first.</p></div>}
    <div className="studio-grid">{items.map(item => <article className="studio-card moderation-card" key={item.storyId}>
      <span className="draft-badge">{item.hiddenForReview ? "Hidden for review" : "In the feed"}</span>
      <h2>{item.title}</h2><p>{item.artistName}</p>
      <p data-testid="report-summary">{item.openReports} {item.openReports === 1 ? "report" : "reports"} · {item.reasons.map(reasonLabel).join(" · ")}</p>
      {item.notes.length > 0 && <ul className="moderation-notes">{item.notes.slice(0, 5).map((note, index) => <li key={index}>{note}</li>)}</ul>}
      {!item.hiddenForReview && <p><Link className="text-link" href={`/story/${item.storyId}`}>View story ↗</Link></p>}
      <div className="subtitle-actions">
        {item.hiddenForReview
          ? <button className="retry-button" type="button" onClick={() => void act(item, "restore")}>Restore to feed</button>
          : <button className="retry-button danger-button" type="button" onClick={() => void act(item, "hide")}>Hide for review</button>}
        <button className="text-control" type="button" onClick={() => void act(item, "resolve")}>{item.hiddenForReview ? "Close reports, keep hidden" : "Close reports, no action"}</button>
      </div>
    </article>)}</div>
  </>;
}
