// Mazwi service worker. It keeps the app installable and opens shared recordings in Upload.
// It never caches API responses or recordings: only hashed static files, icons and the offline page.
const SHELL = "mazwi-shell-v1";
const SHARE = "mazwi-share";
const OFFLINE = "/offline.html";

self.addEventListener("install", event => {
  event.waitUntil(caches.open(SHELL).then(cache => cache.addAll([OFFLINE, "/icons/icon-192.png"])).then(() => self.skipWaiting()));
});

self.addEventListener("activate", event => {
  event.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(key => key.startsWith("mazwi-shell-") && key !== SHELL).map(key => caches.delete(key))))
    .then(() => self.clients.claim()));
});

async function receiveShare(request) {
  const form = await request.formData();
  const file = form.get("recording");
  if (file instanceof File && file.size > 0) {
    const cache = await caches.open(SHARE);
    await cache.put("/shared-recording", new Response(file, {
      headers: { "Content-Type": file.type || "application/octet-stream", "X-File-Name": encodeURIComponent(file.name || "shared-recording") },
    }));
  }
  return Response.redirect("/upload?shared=1", 303);
}

self.addEventListener("fetch", event => {
  const url = new URL(event.request.url);
  if (url.origin !== self.location.origin) return;
  if (event.request.method === "POST" && url.pathname === "/share-target") {
    event.respondWith(receiveShare(event.request));
    return;
  }
  if (event.request.method !== "GET" || url.pathname.startsWith("/api/")) return;
  if (url.pathname.startsWith("/_next/static/") || url.pathname.startsWith("/icons/")) {
    event.respondWith(caches.open(SHELL).then(async cache => {
      const cached = await cache.match(event.request);
      if (cached) return cached;
      const response = await fetch(event.request);
      if (response.ok) await cache.put(event.request, response.clone());
      return response;
    }));
    return;
  }
  if (event.request.mode === "navigate") {
    event.respondWith(fetch(event.request).catch(async () => (await caches.match(OFFLINE)) ?? Response.error()));
  }
});
