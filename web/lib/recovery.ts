import { accountError } from "./account.ts";

async function post(path: string, body?: unknown): Promise<Response> {
  return fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: body === undefined ? undefined : JSON.stringify(body) });
}

function linkError(response: Response, action: string): Error {
  if (response.status === 400) return new Error("This link has expired or was already used. Request a new one.");
  if (response.status === 429) return new Error("Too many attempts. Wait a minute, then try again.");
  return new Error(`We couldn’t ${action}. Please try again.`);
}

// Always answers the same way, so nobody can use it to find out who has an account.
export async function requestPasswordReset(email: string): Promise<void> {
  const invalid = accountError(email, "x", "signin");
  if (invalid) throw new Error(invalid);
  const response = await post("/api/account/password-reset", { email: email.trim() });
  if (response.status === 429) throw new Error("Too many attempts. Wait a minute, then try again.");
  if (!response.ok) throw new Error("We couldn’t send the email. Please try again.");
}

export async function resetPassword(token: string, password: string, repeated: string): Promise<void> {
  if (!token) throw new Error("This link is incomplete. Open the link from your email again.");
  if (password !== repeated) throw new Error("The two passwords do not match.");
  const invalid = accountError("reset@mazwi.local", password, "signup");
  if (invalid) throw new Error(invalid);
  const response = await post("/api/account/password-reset/confirm", { token, password });
  if (!response.ok) throw linkError(response, "change your password");
}

export async function verifyEmail(token: string): Promise<void> {
  if (!token) throw new Error("This link is incomplete. Open the link from your email again.");
  const response = await post("/api/account/verify-email", { token });
  if (!response.ok) throw linkError(response, "confirm your email");
}

export async function resendVerification(): Promise<void> {
  const response = await post("/api/account/verification-email");
  if (response.status === 401) throw new Error("Sign in again to send a new link.");
  if (response.status === 429) throw new Error("Too many attempts. Wait a minute, then try again.");
  if (!response.ok) throw new Error("We couldn’t send the email. Please try again.");
}
