export type UploadedStory = { id: string; title: string; artistName: string; status: "unpublished" };

export function recordingError(file: File | null): string | null {
  if (!file || !file.size) return "Choose a non-empty recording.";
  if (file.size > 25_000_000) return "Your recording must be 25 MB or smaller.";
  if (!/\.(mp3|wav|m4a|mp4|webm)$/i.test(file.name)) return "Choose an MP3, WAV, M4A, MP4, or WebM recording.";
  return null;
}

export async function uploadStory(form: FormData, signal?: AbortSignal): Promise<UploadedStory> {
  const response = await fetch("/api/stories", { method: "POST", body: form, signal });
  if (!response.ok) {
    if (response.status === 401) throw new Error("You have been signed out. Sign in again; your form has been kept.");
    if (response.status === 413) throw new Error("Your recording is too large. Choose a file of 25 MB or smaller.");
    if (response.status === 400) {
      const problem = await response.json().catch(() => null);
      throw new Error(typeof problem?.detail === "string" ? problem.detail.slice(0, 300) : "Check all fields, the recording and the permission checkbox.");
    }
    throw new Error("We couldn’t save your story. Please try again. Your form has been kept.");
  }
  const story = await response.json();
  if (typeof story.id !== "string" || typeof story.title !== "string" || typeof story.artistName !== "string" || story.status !== "unpublished") {
    throw new Error("We couldn’t confirm the upload. Please check the API before uploading again.");
  }
  return story;
}
