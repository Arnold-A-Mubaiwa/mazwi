export const REPORT_REASONS = [
  { value: "NoPermission", label: "Shared without permission" },
  { value: "Harmful", label: "Hateful, violent or harmful" },
  { value: "Misleading", label: "Wrong translation or misleading label" },
  { value: "Other", label: "Something else" },
] as const;
export type ReportReason = (typeof REPORT_REASONS)[number]["value"];
export type ReportedStory = { storyId: string; title: string; artistName: string; hiddenForReview: boolean; openReports: number; reasons: string[]; notes: string[]; latestReportAt: string };
export type ModerationAction = "hide" | "restore" | "resolve";

export function reasonLabel(value: string): string {
  return REPORT_REASONS.find(reason => reason.value === value)?.label ?? value;
}

export async function reportStory(id: string, reason: ReportReason, note: string): Promise<void> {
  if (!REPORT_REASONS.some(item => item.value === reason)) throw new Error("Choose a reason.");
  if (note.length > 500) throw new Error("Keep the note to 500 characters.");
  const response = await fetch(`/api/feed/${encodeURIComponent(id)}/reports`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ reason, note: note.trim() || null }),
  });
  if (response.status === 202) return;
  if (response.status === 404) throw new Error("This story is no longer in the feed.");
  if (response.status === 429) throw new Error("Too many reports from here. Wait a minute, then try again.");
  throw new Error("The report could not be sent. Please try again.");
}

function isReported(value: unknown): value is ReportedStory {
  const item = value as Partial<ReportedStory> | null;
  return !!item && typeof item.storyId === "string" && typeof item.title === "string" && typeof item.artistName === "string"
    && typeof item.hiddenForReview === "boolean" && Number.isInteger(item.openReports) && Array.isArray(item.reasons) && Array.isArray(item.notes)
    && typeof item.latestReportAt === "string";
}

export async function getReports(signal?: AbortSignal): Promise<ReportedStory[]> {
  const response = await fetch("/api/moderation/reports", { cache: "no-store", signal });
  if (response.status === 403) throw new Error("Only Mazwi moderators can see reports.");
  if (response.status === 401) throw new Error("Sign in as a moderator to see reports.");
  if (!response.ok) throw new Error("Reports could not be loaded. Please try again.");
  const value: unknown = await response.json();
  if (!Array.isArray(value) || !value.every(isReported)) throw new Error("The API returned unexpected reports.");
  return value;
}

export async function moderate(id: string, action: ModerationAction): Promise<void> {
  const response = await fetch(`/api/moderation/stories/${encodeURIComponent(id)}/${action}`, { method: "POST" });
  if (response.status === 204) return;
  if (response.status === 404) throw new Error("That story no longer exists.");
  if (response.status === 403) throw new Error("Only Mazwi moderators can do this.");
  throw new Error("That did not work. Please try again.");
}
