import { StoryStudio } from "@/components/story-studio";

export const metadata = { title: "Your local studio | Mazwi" };
export default function StudioPage() {
  return <main id="main" className="studio-page"><p className="eyebrow">A PLACE FOR YOUR WORDS</p><h1>Your stories,<br /><em>at a glance.</em></h1><p className="studio-intro">All stories saved on this computer. Open an editor to check the words, recordings and approval for each language.</p><StoryStudio /></main>;
}
