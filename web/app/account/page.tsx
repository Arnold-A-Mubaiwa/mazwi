import { AccountSettings } from "@/components/account-settings";
import { RequireArtist } from "@/components/require-artist";

export const metadata = { title: "Your account | Mazwi" };
export default function AccountPage() {
  return <main id="main" className="upload-page account-page">
    <p className="eyebrow">YOUR ACCOUNT</p>
    <h1>Your voice,<br /><em>your decision.</em></h1>
    <p className="upload-intro">You can unpublish or delete any story from its editor. Here you can remove everything at once.</p>
    <RequireArtist><AccountSettings /></RequireArtist>
  </main>;
}
