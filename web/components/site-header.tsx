import Link from "next/link";
import { Brand } from "./brand";
import { FEATURES } from "@/lib/config";
import { AccountLink } from "./account-link";
export function SiteHeader() {
  return <header className="site-header"><Brand /><nav aria-label="Main navigation">
    <Link href="/#stories">Explore</Link>{FEATURES.upload && <Link href="/upload">Upload</Link>}<Link href="/studio">Studio</Link><AccountLink /><Link href="/about">Our story <span aria-hidden="true">↗</span></Link>
  </nav></header>;
}
