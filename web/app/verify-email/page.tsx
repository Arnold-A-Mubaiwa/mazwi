import { VerifyEmail } from "@/components/recovery-forms";

export const metadata = { title: "Confirm your email | Mazwi" };
export default async function VerifyEmailPage({ searchParams }: { searchParams: Promise<{ token?: string | string[] }> }) {
  const { token } = await searchParams;
  return <main id="main" className="upload-page account-page">
    <p className="eyebrow">ALMOST THERE</p>
    <h1>Confirming your<br /><em>email.</em></h1>
    <VerifyEmail token={typeof token === "string" ? token : ""} />
  </main>;
}
