import { notFound } from "next/navigation";
import { RequireArtist } from "@/components/require-artist";
import { StoryDraftEditor } from "@/components/story-draft-editor";
import { FEATURES } from "@/lib/config";

export const metadata = { title: "Your story drafts | Mazwi" };

export default async function StoryReviewPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!FEATURES.manualEditing || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) notFound();
  return <main id="main" className="draft-page">
    <p className="eyebrow">YOUR WORDS. STILL YOUR SAY.</p>
    <h1>A story, <em>in your words.</em></h1>
    <p className="draft-intro">Write the original. Share its meaning in another language. Save your work here before the next step.</p>
    <RequireArtist><StoryDraftEditor id={id} /></RequireArtist>
  </main>;
}
