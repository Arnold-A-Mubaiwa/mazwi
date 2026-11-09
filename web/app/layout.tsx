import type { Metadata } from "next";
import { SiteHeader } from "@/components/site-header";
import "./globals.css";
export const metadata: Metadata = {
  title: { default: "Mazwi — Every African story, in your language", template: "%s · Mazwi" },
  description: "A home for African stories, the people who tell them, and the languages that connect us.",
};
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body className="min-h-screen antialiased">
    <a className="skip-link" href="#main">Skip to content</a>
    <div className="page-shell"><SiteHeader />{children}
      <footer className="site-footer"><span>Words. Voices. Belonging.</span><span>Rooted in Africa. Shared with care.</span></footer>
    </div>
  </body></html>;
}
