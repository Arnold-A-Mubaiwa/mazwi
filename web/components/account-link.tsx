"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { getCurrentArtist, signOut, type Artist } from "@/lib/account";

export function AccountLink() {
  const pathname = usePathname();
  const router = useRouter();
  const [artist, setArtist] = useState<Artist | null | undefined>(undefined);
  useEffect(() => {
    const controller = new AbortController();
    getCurrentArtist(controller.signal).then(setArtist).catch(() => { if (!controller.signal.aborted) setArtist(null); });
    return () => controller.abort();
  }, [pathname]);
  if (artist === undefined) return null;
  if (!artist) return <Link href={`/signin?next=${encodeURIComponent(pathname)}`}>Sign in</Link>;
  return <>{artist.isModerator && <Link href="/moderation">Moderation</Link>}<Link href="/account">Account</Link><button type="button" className="header-signout" title={`Signed in as ${artist.email}`}
    onClick={() => signOut().then(() => { setArtist(null); router.replace("/"); router.refresh(); }).catch(() => {})}>Sign out</button></>;
}
