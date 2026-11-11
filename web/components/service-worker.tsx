"use client";

import { useEffect } from "react";

// Registers /sw.js so Mazwi can be installed and receive shared recordings. Browsers allow it only on HTTPS or localhost.
export function ServiceWorker() {
  useEffect(() => {
    // Only production builds: in development, chunk names are not content-hashed and would be served stale from the cache.
    if (process.env.NODE_ENV !== "production" || !("serviceWorker" in navigator) || !window.isSecureContext) return;
    navigator.serviceWorker.register("/sw.js", { scope: "/" }).catch(() => {});
  }, []);
  return null;
}
