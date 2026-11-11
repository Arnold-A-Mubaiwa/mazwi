import { readDraft, type StoryDraft } from "./story-drafts.ts";

// Deleting is permanent, so the artist types the story's title to confirm (spacing and case are forgiven).
export function titleConfirmed(typed: string, title: string): boolean {
  const normalise = (value: string) => value.trim().replace(/\s+/g, " ").toLocaleLowerCase();
  return normalise(typed).length > 0 && normalise(typed) === normalise(title);
}

export async function unpublishStory(id: string, reviewVersion: string, signal?: AbortSignal): Promise<StoryDraft> {
  return readDraft(await fetch(`/api/stories/${encodeURIComponent(id)}/unpublish`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reviewVersion }), signal,
  }), id);
}

export async function deleteStory(id: string, reviewVersion: string, signal?: AbortSignal): Promise<void> {
  const response = await fetch(`/api/stories/${encodeURIComponent(id)}?version=${encodeURIComponent(reviewVersion)}`, { method: "DELETE", signal });
  if (response.status === 204) return;
  if (response.status === 404) throw new Error("This story was already deleted.");
  if (response.status === 409) throw new Error("This story changed since you opened it. Reload the page, check it, then delete again.");
  if (response.status === 401) throw new Error("You have been signed out. Sign in again to delete this story.");
  if (response.status === 403) throw new Error("This story belongs to another artist.");
  throw new Error("The story could not be deleted. Nothing was removed. Please try again.");
}

export async function deleteAccount(password: string, signal?: AbortSignal): Promise<void> {
  if (!password) throw new Error("Enter your password to delete your account.");
  const response = await fetch("/api/account/delete", {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ password }), signal,
  });
  if (response.status === 204) return;
  if (response.status === 400) throw new Error("That password did not match. Your account was not deleted.");
  if (response.status === 401) throw new Error("You have been signed out. Sign in again to delete your account.");
  if (response.status === 429) throw new Error("Too many attempts. Wait a minute, then try again.");
  throw new Error("Your account could not be deleted. Please try again.");
}
