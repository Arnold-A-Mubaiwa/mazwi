import { AccountForm } from "@/components/account-form";
import { safeNext } from "@/lib/account";

export const metadata = { title: "Sign in | Mazwi" };
export default async function SignInPage({ searchParams }: { searchParams: Promise<{ next?: string | string[] }> }) {
  const { next } = await searchParams;
  return <main id="main" className="upload-page account-page">
    <p className="eyebrow">WELCOME BACK</p>
    <h1>Your stories,<br /><em>still yours.</em></h1>
    <p className="upload-intro">Sign in to upload, edit and approve your stories. Viewers never need an account.</p>
    <AccountForm mode="signin" next={safeNext(next)} />
  </main>;
}
