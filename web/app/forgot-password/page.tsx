import { ForgotPasswordForm } from "@/components/recovery-forms";

export const metadata = { title: "Forgot your password | Mazwi" };
export default function ForgotPasswordPage() {
  return <main id="main" className="upload-page account-page">
    <p className="eyebrow">IT HAPPENS</p>
    <h1>Forgot your<br /><em>password?</em></h1>
    <p className="upload-intro">Enter the email you signed up with. We will send a link to choose a new password.</p>
    <ForgotPasswordForm />
  </main>;
}
