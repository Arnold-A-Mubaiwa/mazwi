import { notFound } from "next/navigation";
import { RequireArtist } from "@/components/require-artist";
import { UploadForm } from "@/components/upload-form";
import { FEATURES } from "@/lib/config";

export const metadata = { title: "Share your story | Mazwi" };

export default function UploadPage() {
  if (!FEATURES.upload) notFound();
  return <main id="main" className="upload-page">
    <p className="eyebrow">THE FIRST STEP IS YOUR VOICE</p>
    <h1>A story only<br /><em>you can tell.</em></h1>
    <p className="upload-intro">A poem, a song, a tale passed down. Start with the original recording, in your own language.</p>
    <div className="upload-layout"><RequireArtist><UploadForm /></RequireArtist><aside className="upload-aside">
      <p className="eyebrow">YOUR WORDS. YOUR SAY.</p>
      <h2>Saved here.<br />Not shared yet.</h2>
      <p>Your recording stays on this computer. Uploading does not publish it or send it to an AI service.</p>
      <p>You can write a transcript and manual translations after uploading. Artist approval is coming next. Your voice will never be cloned from this permission.</p>
      <p className="upload-local-note">Local demo · Use recordings you own or have permission to share.</p>
    </aside></div>
  </main>;
}
