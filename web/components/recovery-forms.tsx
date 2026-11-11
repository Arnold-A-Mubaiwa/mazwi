"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { getCurrentArtist } from "@/lib/account";
import { requestPasswordReset, resendVerification, resetPassword, verifyEmail } from "@/lib/recovery";

export function ForgotPasswordForm() {
  const [state, setState] = useState<{ busy?: boolean; sent?: boolean; error?: string }>({});
  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const email = String(new FormData(event.currentTarget).get("email") ?? "");
    setState({ busy: true });
    try { await requestPasswordReset(email); setState({ sent: true }); }
    catch (failure) { setState({ error: failure instanceof Error ? failure.message : "Please try again." }); }
  }
  if (state.sent) return <p className="account-sent" role="status">If an account uses that email, we have sent a link to choose a new password. It works once, for one hour. On this local demo the email is saved in the <code>.local/outbox</code> folder.</p>;
  return <form className="upload-form account-form" onSubmit={submit} noValidate>
    <fieldset disabled={state.busy}><legend className="sr-only">Reset your password</legend>
      <label>Email address<input type="email" name="email" maxLength={254} required autoComplete="email" /></label>
      <button className="retry-button" type="submit">{state.busy ? "Sending…" : "Send reset link"}</button>
    </fieldset>
    {state.error && <p className="upload-error" role="alert">{state.error}</p>}
    <p className="account-switch"><Link className="text-link" href="/signin">Back to sign in</Link></p>
  </form>;
}

export function ResetPasswordForm({ token }: { token: string }) {
  const [state, setState] = useState<{ busy?: boolean; done?: boolean; error?: string }>({});
  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setState({ busy: true });
    try { await resetPassword(token, String(form.get("password") ?? ""), String(form.get("repeat") ?? "")); setState({ done: true }); }
    catch (failure) { setState({ error: failure instanceof Error ? failure.message : "Please try again." }); }
  }
  if (state.done) return <p className="account-sent" role="status">Your password has changed and every device was signed out. <Link className="text-link" href="/signin">Sign in with your new password</Link></p>;
  return <form className="upload-form account-form" onSubmit={submit} noValidate>
    <fieldset disabled={state.busy}><legend className="sr-only">Choose a new password</legend>
      <label><span id="new-password-label">New password</span><input type="password" name="password" maxLength={128} required autoComplete="new-password" aria-labelledby="new-password-label" aria-describedby="new-password-help" /><span id="new-password-help">At least 10 characters.</span></label>
      <label>Repeat new password<input type="password" name="repeat" maxLength={128} required autoComplete="new-password" /></label>
      <button className="retry-button" type="submit">{state.busy ? "Saving…" : "Change password"}</button>
    </fieldset>
    {state.error && <p className="upload-error" role="alert">{state.error}{state.error.includes("Request a new one") && <> <Link className="text-link" href="/forgot-password">Request a new link</Link></>}</p>}
  </form>;
}

// Confirms once when the page opens, then shows the result.
export function VerifyEmail({ token }: { token: string }) {
  const [state, setState] = useState<"working" | "done" | string>("working");
  const started = useRef(false);
  useEffect(() => {
    if (started.current) return;
    started.current = true;
    verifyEmail(token).then(() => setState("done")).catch(failure => setState(failure instanceof Error ? failure.message : "Please try again."));
  }, [token]);
  if (state === "working") return <p role="status">Confirming your email…</p>;
  if (state === "done") return <p className="account-sent" role="status">Email confirmed. Thank you. <Link className="text-link" href="/studio">Go to your studio</Link></p>;
  return <p className="upload-error" role="alert">{state} You can send a new link from your studio.</p>;
}

// Reminds a signed-in artist to confirm their email, with a button to send the link again.
export function VerifyBanner() {
  const [unverified, setUnverified] = useState(false);
  const [message, setMessage] = useState("");
  useEffect(() => {
    const controller = new AbortController();
    getCurrentArtist(controller.signal).then(artist => setUnverified(Boolean(artist && !artist.emailVerified))).catch(() => {});
    return () => controller.abort();
  }, []);
  if (!unverified) return null;
  return <div className="verify-banner" role="region" aria-label="Confirm your email">
    <p>Please confirm your email address so you can always get back into your account.</p>
    <button className="text-control" type="button" onClick={() => resendVerification().then(() => setMessage("We sent a new link. On this local demo it is in the .local/outbox folder."), failure => setMessage(failure instanceof Error ? failure.message : "Please try again."))}>Send the link again</button>
    {message && <p role="status">{message}</p>}
  </div>;
}
