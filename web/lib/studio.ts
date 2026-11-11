export type StudioStory = { id: string; title: string; artistName: string; community: string; language: string; status: "unpublished" | "published"; hiddenForReview?: boolean };
export type StudioPage = { items: StudioStory[]; nextOffset: number | null };
export async function getStudio(offset = 0, signal?: AbortSignal): Promise<StudioPage> {
  const response = await fetch(`/api/studio?offset=${offset}&limit=20`, { cache: "no-store", signal });
  if (response.status === 401) throw new Error("You have been signed out. Sign in again to see your stories.");
  if (!response.ok) throw new Error("Your local stories could not be loaded. Please try again.");
  const page = await response.json();
  if (!page || !Array.isArray(page.items) || !page.items.every((story: StudioStory) => story && [story.id, story.title, story.artistName, story.community, story.language].every(value => typeof value === "string") && ["unpublished", "published"].includes(story.status))
    || (page.nextOffset !== null && (!Number.isInteger(page.nextOffset) || page.nextOffset <= offset))) throw new Error("The API returned unexpected local stories.");
  return page;
}
