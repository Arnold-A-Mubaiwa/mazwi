export type Artist = { id: string; email: string; emailVerified?: boolean; isModerator?: boolean };
export type AccountMode = "signin" | "signup";

export function accountError(email: string, password: string, mode: AccountMode): string | null {
  const address = email.trim();
  if (!address || address.length > 254 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(address)) return "Enter a valid email address.";
  if (!password) return "Enter your password.";
  if (password.length > 128) return "Passwords are at most 128 characters.";
  if (mode === "signup" && (password.length < 10 || !password.trim())) return "Choose a password of 10–128 characters.";
  return null;
}

// Only same-site paths are allowed after sign-in, so a link cannot send the artist to another website.
export function safeNext(value: unknown): string {
  return typeof value === "string" && /^\/(?![/\\])[^\s\\]*$/.test(value) && !value.startsWith("/signin") && !value.startsWith("/signup") ? value : "/studio";
}

function readArtist(value: unknown): Artist {
  const artist = value as Partial<Artist> | null;
  if (!artist || typeof artist.id !== "string" || typeof artist.email !== "string") throw new Error("The API returned an unexpected account.");
  return { id: artist.id, email: artist.email, emailVerified: artist.emailVerified === true, isModerator: artist.isModerator === true };
}

export async function submitAccount(mode: AccountMode, email: string, password: string, signal?: AbortSignal): Promise<Artist> {
  const response = await fetch(`/api/account/${mode}`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: email.trim(), password }), signal,
  });
  if (!response.ok) {
    if (response.status === 401) throw new Error("That email and password do not match an account.");
    if (response.status === 409) throw new Error("An account with this email address already exists. Sign in instead.");
    if (response.status === 429) throw new Error("Too many attempts. Wait a minute, then try again.");
    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      throw new Error(typeof problem?.detail === "string" ? problem.detail.slice(0, 300) : "Check your email address and password.");
    }
    throw new Error("We couldn’t reach your account. Please try again.");
  }
  return readArtist(await response.json());
}

// Returns null when nobody is signed in.
export async function getCurrentArtist(signal?: AbortSignal): Promise<Artist | null> {
  const response = await fetch("/api/account/me", { cache: "no-store", signal });
  if (response.status === 401) return null;
  if (!response.ok) throw new Error("We couldn’t check your account. Please try again.");
  return readArtist(await response.json());
}

export async function signOut(): Promise<void> {
  const response = await fetch("/api/account/signout", { method: "POST" });
  if (!response.ok) throw new Error("We couldn’t sign you out. Please try again.");
}
