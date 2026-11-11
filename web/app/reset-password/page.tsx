import { ResetPasswordForm } from "@/components/recovery-forms";

export const metadata = { title: "Choose a new password | Mazwi" };
export default async function ResetPasswordPage({ searchParams }: { searchParams: Promise<{ token?: string | string[] }> }) {
  const { token } = await searchParams;
  return <main id="main" className="upload-page account-page">
    <p className="eyebrow">A FRESH START</p>
    <h1>Choose a new<br /><em>password.</em></h1>
    <p className="upload-intro">Changing your password signs you out on every device.</p>
    <ResetPasswordForm token={typeof token === "string" ? token : ""} />
  </main>;
}
