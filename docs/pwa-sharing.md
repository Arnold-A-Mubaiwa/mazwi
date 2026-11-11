# Installable app and sharing

Issue #37 makes Mazwi installable, adds a **Share this story** button, and lets an installed Mazwi receive recordings shared from other apps.

## Installing

`app/manifest.ts` serves `/manifest.webmanifest`, which sets standalone display, the dark theme colour and PNG icons (192, 512 and a maskable 512). `/icons/icon.svg` is the source; the PNGs were rendered from it with Playwright's browser. On an HTTPS origin, browsers offer **Install** or **Add to Home screen**.

## Service worker

`public/sw.js` is registered by `components/service-worker.tsx`, and only in a secure context (HTTPS or localhost). It deliberately does little:
- It caches the offline page, the icons and hashed `/_next/static/` files.
- Pages are always fetched from the network; `/offline.html` is shown only when there is no connection.
- It never touches `/api/`: story data, recordings and account requests are not cached on the device.
- `/sw.js` is served with `no-store`, so updates reach installed apps. All pages also get `X-Content-Type-Options: nosniff` and a strict `Referrer-Policy`.

## Sharing a story

Story cards have **Share this story**. On phones it opens the share sheet with the title, the artist and the story link. Where there is no share sheet it copies the link instead, and if that also fails it shows the link to copy by hand. Cancelling the share sheet does nothing.

## Receiving a recording from another app

The manifest's `share_target` puts an installed Mazwi in the share sheet for audio and video. When an artist shares a recording to it:
1. The service worker receives the shared file at `/share-target`.
2. It keeps the file in a private browser cache on that device; it is not uploaded yet.
3. It opens **Upload**, after sign-in if needed.
4. Upload fills in the recording field and shows "Shared recording ready: …". The file is removed from the cache as soon as it is read.

The artist still fills in the details and gives permission before anything is saved. Unsupported types show the usual recording error.

## Tests

- `tests/share.test.mjs` covers:
  - the share sheet, cancelling it, the clipboard fallback and total failure;
  - the result messages;
  - reading a shared file once;
  - the manifest's icons and share target.
- `e2e/pwa.spec.ts` covers:
  - the manifest link and PNG icons, and the `no-store` worker;
  - the worker taking control, with no `/api` entry in any cache;
  - a multipart POST to `/share-target` landing in Upload with the file filled in, and gone after a reload;
  - the Share button with a stubbed share sheet and with the clipboard fallback.

## Limits (not checked on a real device)

- Android only offers the share target for an **installed** app on an **HTTPS** origin. This localhost demo cannot be installed from a phone, so the real share sheet hand-off is unverified. The test posts the same multipart form the OS would send.
- iOS Safari does not support Web Share Target; iPhone users upload from the Upload page.
- The Android "bubble" from the original plan is not defined and is not included.
