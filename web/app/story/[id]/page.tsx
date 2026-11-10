import Link from "next/link";
import { notFound } from "next/navigation";
import { LanguagePicker } from "@/components/language-picker";
import { StoryFeed } from "@/components/story-feed";

export const metadata = { title: "An African story | Mazwi" };
export default async function StoryPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) notFound();
  return <main id="main" className="story-page"><Link className="text-link" href="/#stories">Back to the voices</Link><div className="story-language"><LanguagePicker /></div><StoryFeed storyId={id} /></main>;
}
