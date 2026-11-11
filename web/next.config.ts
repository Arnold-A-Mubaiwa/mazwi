import type { NextConfig } from "next";

const apiUrl = new URL(process.env.MAZWI_API_URL ?? "http://127.0.0.1:5046");
if (!["http:", "https:"].includes(apiUrl.protocol) || apiUrl.username || apiUrl.password) {
  throw new Error("MAZWI_API_URL must be an HTTP(S) URL without credentials.");
}

const config: NextConfig = {
  poweredByHeader: false,
  experimental: { proxyClientMaxBodySize: 26_000_000 },
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl.origin}/api/:path*` }];
  },
  async headers() {
    return [
      { source: "/(.*)", headers: [{ key: "X-Content-Type-Options", value: "nosniff" }, { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" }] },
      // The service worker must always be fetched fresh so updates reach installed apps.
      { source: "/sw.js", headers: [
        { key: "Content-Type", value: "application/javascript; charset=utf-8" },
        { key: "Cache-Control", value: "no-cache, no-store, must-revalidate" },
        { key: "Content-Security-Policy", value: "default-src 'self'; script-src 'self'" },
      ] },
    ];
  },
};

export default config;
