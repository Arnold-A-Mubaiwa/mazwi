"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { getCurrentArtist, type Artist } from "@/lib/account";
import { deleteAccount } from "@/lib/removal";

export function AccountSettings() {
  const router = useRouter();
  const [artist, setArtist] = useState<Artist | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const controller = new AbortController();
    getCurrentArtist(controller.signal).then(setArtist).catch(() => {});
    return () => controller.abort();
  }, []);

  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    if (form.get("understand") !== "yes") { setError("Tick the box to confirm you understand this cannot be undone."); return; }
    setBusy(true); setError(null);
    try {
      await deleteAccount(String(form.get("password") ?? ""));
      router.replace("/");
      router.refresh();
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "Your account could not be deleted.");
      setBusy(false);
    }
  }

  return <>
    {artist && <p className="account-email">Signed in as <strong>{artist.email}</strong></p>}
    <form className="upload-form account-form" onSubmit={submit} noValidate>
      <fieldset disabled={busy}>
        <legend><h2>Delete my account</h2></legend>
        <p className="draft-notice">This permanently deletes your account and every story you uploaded, with all translations and recordings. It cannot be undone.</p>
        <label>Current password<input type="password" name="password" maxLength={128} autoComplete="current-password" required /></label>
        <label className="permission-field"><input type="checkbox" name="understand" value="yes" /><span>I understand my stories and recordings will be deleted for good.</span></label>
        <button className="retry-button danger-button" type="submit">{busy ? "Deleting…" : "Delete my account and stories"}</button>
      </fieldset>
      {error && <p className="upload-error" role="alert">{error}</p>}
    </form>
  </>;
}
