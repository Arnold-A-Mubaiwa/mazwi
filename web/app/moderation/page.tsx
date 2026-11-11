import { ModerationQueue } from "@/components/moderation";
import { RequireArtist } from "@/components/require-artist";

export const metadata = { title: "Moderation | Mazwi" };
export default function ModerationPage() {
  return <main id="main" className="studio-page">
    <p className="eyebrow">CARE FOR THE COMMUNITY</p>
    <h1>Reported<br /><em>stories.</em></h1>
    <p className="studio-intro">Hide a story while you look into it, restore it if it is fine, or close the reports. Artists see when their story is hidden.</p>
    <RequireArtist><ModerationQueue /></RequireArtist>
  </main>;
}
