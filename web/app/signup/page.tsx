import { AccountForm } from "@/components/account-form";
import { safeNext } from "@/lib/account";

export const metadata = { title: "Create your account | Mazwi" };
export default async function SignUpPage({ searchParams }: { searchParams: Promise<{ next?: string | string[] }> }) {
  const { next } = await searchParams;
  return <main id="main" className="upload-page account-page">
    <p className="eyebrow">A HOME FOR YOUR VOICE</p>
    <h1>Every story needs<br /><em>its teller.</em></h1>
    <p className="upload-intro">Create an artist account. Only you can edit or approve the stories you upload.</p>
    <AccountForm mode="signup" next={safeNext(next)} />
  </main>;
}
