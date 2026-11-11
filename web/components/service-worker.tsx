"use client";

import { useEffect } from "react";

// Registers /sw.js so Mazwi can be installed and receive shared recordings. Browsers allow it only on HTTPS or localhost.
export function ServiceWorker() {
  useEffect(() => {
    if (!("serviceWorker" in navigator) || !window.isSecureContext) return;
    navigator.serviceWorker.register("/sw.js", { scope: "/" }).catch(() => {});
  }, []);
  return null;
}
