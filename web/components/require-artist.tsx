"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { getCurrentArtist } from "@/lib/account";

// Shows its children only to a signed-in artist; everyone else is sent to sign in and brought back afterwards.
export function RequireArtist({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [state, setState] = useState<"checking" | "ready" | "error">("checking");
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    getCurrentArtist(controller.signal).then(artist => {
      if (artist) setState("ready");
      else router.replace(`/signin?next=${encodeURIComponent(pathname)}`);
    }).catch(() => { if (!controller.signal.aborted) setState("error"); });
    return () => controller.abort();
  }, [pathname, router, retry]);
  if (state === "ready") return <>{children}</>;
  if (state === "error") return <section><p role="alert">We couldn’t check your account. Please try again.</p><button className="retry-button" onClick={() => { setState("checking"); setRetry(value => value + 1); }}>Try again</button></section>;
  return <p role="status">Checking your account…</p>;
}
