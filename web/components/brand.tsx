import Link from "next/link";
export function Brand() {
  return <Link className="brand" href="/" aria-label="Mazwi home">
    <svg viewBox="0 0 36 36" aria-hidden="true" fill="none"><path d="M6 23V13M14 29V7M22 25V11M30 20V16" stroke="currentColor" strokeWidth="4" strokeLinecap="round" /></svg>
    <span>mazwi<span className="brand-dot">.</span></span>
  </Link>;
}
