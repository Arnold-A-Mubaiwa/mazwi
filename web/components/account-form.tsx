"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { accountError, submitAccount, type AccountMode } from "@/lib/account";

export function AccountForm({ mode, next }: { mode: AccountMode; next: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const signup = mode === "signup";

  async function submit(event: React.SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const email = String(form.get("email") ?? "");
    const password = String(form.get("password") ?? "");
    const invalid = accountError(email, password, mode);
    if (invalid) { setError(invalid); return; }
    setError(null);
    setBusy(true);
    controller.current = new AbortController();
    try {
      await submitAccount(mode, email, password, controller.current.signal);
      router.replace(next);
      router.refresh();
    } catch (failure) {
      if (!controller.current.signal.aborted) setError(failure instanceof Error ? failure.message : "Please try again.");
      setBusy(false);
    }
  }

  const other = `${signup ? "/signin" : "/signup"}?next=${encodeURIComponent(next)}`;
  return <form className="upload-form account-form" onSubmit={submit} noValidate>
    <fieldset disabled={busy}>
      <legend className="sr-only">{signup ? "Create your artist account" : "Sign in to your artist account"}</legend>
      <label>Email address<input type="email" name="email" maxLength={254} required autoComplete="email" /></label>
      <label><span id="password-label">Password</span><input type="password" name="password" maxLength={128} required aria-labelledby="password-label"
        aria-describedby={signup ? "password-help" : undefined} autoComplete={signup ? "new-password" : "current-password"} />
      {signup && <span id="password-help">At least 10 characters.</span>}</label>
      <button className="retry-button" type="submit">{busy ? "One moment…" : signup ? "Create account" : "Sign in"}</button>
    </fieldset>
    {error && <p className="upload-error" role="alert">{error}</p>}
    <p className="account-switch">{signup ? "Already have an account?" : "New to Mazwi?"} <Link className="text-link" href={other}>{signup ? "Sign in instead" : "Create an account"}</Link></p>
  </form>;
}
