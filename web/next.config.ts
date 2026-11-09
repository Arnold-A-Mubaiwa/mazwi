import type { NextConfig } from "next";

const apiUrl = new URL(process.env.MAZWI_API_URL ?? "http://127.0.0.1:5046");
if (!["http:", "https:"].includes(apiUrl.protocol) || apiUrl.username || apiUrl.password) {
  throw new Error("MAZWI_API_URL must be an HTTP(S) URL without credentials.");
}

const config: NextConfig = {
  poweredByHeader: false,
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl.origin}/api/:path*` }];
  },
};

export default config;
