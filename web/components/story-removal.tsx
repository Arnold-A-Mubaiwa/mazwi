"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { deleteStory, titleConfirmed, unpublishStory } from "@/lib/removal";
import type { StoryDraft } from "@/lib/story-drafts";

type Props = { draft: StoryDraft; disabled: boolean; onChanged: (draft: StoryDraft, message: string) => void; onError: (message: string) => void };

export function StoryRemoval({ draft, disabled, onChanged, onError }: Props) {
  const router = useRouter();
  const [typed, setTyped] = useState("");
  const [busy, setBusy] = useState(false);

  async function unpublish() {
    setBusy(true);
    try { onChanged(await unpublishStory(draft.id, draft.reviewVersion), "Story unpublished. Viewers no longer see it. Approve the original again to publish it."); }
    catch (failure) { onError(failure instanceof Error ? failure.message : "The story could not be unpublished."); }
    finally { setBusy(false); }
  }

  async function remove(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!titleConfirmed(typed, draft.title)) { onError("Type the story’s title exactly to confirm."); return; }
    setBusy(true);
    try {
      await deleteStory(draft.id, draft.reviewVersion);
      router.replace("/studio");
    } catch (failure) {
      onError(failure instanceof Error ? failure.message : "The story could not be deleted.");
      setBusy(false);
    }
  }

  return <section className="draft-card story-removal" aria-labelledby="removal-heading">
    <p className="eyebrow">03 / YOUR STORY, YOUR CHOICE</p><h2 id="removal-heading">Take it down or delete it.</h2>
    {draft.originalApproved
      ? <><p>Unpublishing removes every approval, so viewers no longer see this story. Your words and recordings are kept.</p>
        <button className="retry-button secondary-button" type="button" disabled={disabled || busy} onClick={() => void unpublish()}>Unpublish story</button></>
      : <p>This story is not published. Viewers cannot see it.</p>}
    <form onSubmit={remove}><fieldset disabled={disabled || busy}>
      <legend>Delete this story permanently</legend>
      <p className="draft-notice">This deletes the story, every translation, subtitle timing and play count, and all its recordings from Mazwi. It cannot be undone. Copies viewers already downloaded cannot be recalled.</p>
      <label htmlFor="delete-confirm">Type the title, <strong>{draft.title}</strong>, to confirm</label>
      <input id="delete-confirm" value={typed} onChange={event => setTyped(event.target.value)} autoComplete="off" />
      <button className="retry-button danger-button" type="submit" disabled={!titleConfirmed(typed, draft.title)}>Delete story forever</button>
    </fieldset></form>
  </section>;
}
