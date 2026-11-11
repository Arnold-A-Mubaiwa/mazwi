import { RequireArtist } from "@/components/require-artist";
import { StoryStudio } from "@/components/story-studio";
import { VerifyBanner } from "@/components/recovery-forms";
import { StudioTwins } from "@/components/studio-twins";
import { FEATURES } from "@/lib/config";

export const metadata = { title: "Your studio | Mazwi" };
export default function StudioPage() {
  return <main id="main" className="studio-page"><p className="eyebrow">A PLACE FOR YOUR WORDS</p><h1>Your stories,<br /><em>at a glance.</em></h1><p className="studio-intro">The stories you have uploaded. Open an editor to check the words, recordings and approval for each language.</p><RequireArtist><VerifyBanner /><StoryStudio />{FEATURES.storyTwins && <StudioTwins />}</RequireArtist></main>;
}
