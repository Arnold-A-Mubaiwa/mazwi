import type { MetadataRoute } from "next";

export const SHARE_TARGET_ACTION = "/share-target";
export const SHARED_RECORDING_FIELD = "recording";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Mazwi: every African story, in your language",
    short_name: "Mazwi",
    description: "African stories told by their artists, approved by them, in your language.",
    id: "/",
    start_url: "/",
    scope: "/",
    display: "standalone",
    background_color: "#151711",
    theme_color: "#151711",
    icons: [
      { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png", purpose: "any" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png", purpose: "any" },
      { src: "/icons/maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
    // An installed Mazwi appears in the phone's share sheet for audio and video; the file opens in Upload.
    share_target: {
      action: SHARE_TARGET_ACTION,
      method: "POST",
      enctype: "multipart/form-data",
      params: { title: "title", text: "text", files: [{ name: SHARED_RECORDING_FIELD, accept: ["audio/*", "video/*", ".mp3", ".wav", ".m4a", ".mp4", ".webm"] }] },
    },
  };
}
