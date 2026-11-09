import Link from "next/link";
import { Brand } from "./brand";
export function SiteHeader() {
  return <header className="site-header"><Brand /><nav aria-label="Main navigation">
    <Link href="/#stories">Explore</Link><Link href="/about">Our story <span aria-hidden="true">↗</span></Link>
  </nav></header>;
}
