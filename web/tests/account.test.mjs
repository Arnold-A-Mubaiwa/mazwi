import test from "node:test";
import assert from "node:assert/strict";
import { accountError, getCurrentArtist, safeNext, signOut, submitAccount } from "../lib/account.ts";

test("AccountError_ValidInput_ReturnsNull", () => {
  assert.equal(accountError(" rudo@example.com ", "long password", "signup"), null);
  assert.equal(accountError("rudo@example.com", "short", "signin"), null);
});
test("AccountError_InvalidEmailOrPassword_ExplainsProblem", () => {
  for (const email of ["", "rudo", "rudo@example", "ru do@example.com", `${"a".repeat(250)}@example.com`]) assert.match(accountError(email, "long password", "signin"), /valid email/);
  assert.match(accountError("rudo@example.com", "", "signin"), /Enter your password/);
  assert.match(accountError("rudo@example.com", "123456789", "signup"), /10.128 characters/);
  assert.match(accountError("rudo@example.com", " ".repeat(12), "signup"), /10.128 characters/);
  assert.match(accountError("rudo@example.com", "p".repeat(129), "signin"), /at most 128/);
});
test("SafeNext_SameSitePaths_AreKept", () => {
  for (const path of ["/studio", "/upload", "/story/abc/review?x=1"]) assert.equal(safeNext(path), path);
});
test("SafeNext_ExternalOrLoopingTargets_FallBackToStudio", () => {
  for (const value of [undefined, null, "", "https://evil.example", "//evil.example", "/\\evil.example", "studio", "/signin", "/signup?next=/x", ["/upload"], "/a b"]) assert.equal(safeNext(value), "/studio");
});
test("SubmitAccount_Success_PostsJsonAndReturnsArtist", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/account/signup"); assert.equal(options.method, "POST"); assert.equal(options.signal, controller.signal);
    assert.deepEqual(JSON.parse(options.body), { email: "rudo@example.com", password: "long password" });
    return Response.json({ id: "artist", email: "rudo@example.com" });
  });
  assert.deepEqual(await submitAccount("signup", " rudo@example.com ", "long password", controller.signal), { id: "artist", email: "rudo@example.com", emailVerified: false });
});
test("SubmitAccount_Failures_ShowSafeMessages", async (t) => {
  const cases = [[401, /do not match/], [409, /already exists/], [429, /Too many attempts/], [500, /couldn.t reach/]];
  for (const [status, message] of cases) {
    t.mock.method(globalThis, "fetch", async () => new Response("", { status }));
    await assert.rejects(submitAccount("signin", "rudo@example.com", "long password"), message);
  }
  t.mock.method(globalThis, "fetch", async () => Response.json({ detail: "Choose a password of 10-128 characters." }, { status: 400 }));
  await assert.rejects(submitAccount("signup", "rudo@example.com", "short"), /Choose a password/);
  t.mock.method(globalThis, "fetch", async () => Response.json({ id: 1 }));
  await assert.rejects(submitAccount("signin", "rudo@example.com", "long password"), /unexpected account/);
});
test("GetCurrentArtist_SignedOutOrIn_ReturnsNullOrArtist", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => { assert.equal(url, "/api/account/me"); assert.equal(options.cache, "no-store"); return new Response("", { status: 401 }); });
  assert.equal(await getCurrentArtist(), null);
  t.mock.method(globalThis, "fetch", async () => Response.json({ id: "artist", email: "rudo@example.com" }));
  assert.equal((await getCurrentArtist()).email, "rudo@example.com");
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 500 }));
  await assert.rejects(getCurrentArtist(), /check your account/);
});
test("SignOut_ServerError_Throws", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => { assert.equal(url, "/api/account/signout"); assert.equal(options.method, "POST"); return new Response(null, { status: 204 }); });
  await signOut();
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 500 }));
  await assert.rejects(signOut(), /sign you out/);
});
test("ProtectedRequests_SignedOutOrOtherArtist_ExplainWhatToDo", async (t) => {
  const { readDraft } = await import("../lib/story-drafts.ts");
  const { getStudio } = await import("../lib/studio.ts");
  const { uploadStory } = await import("../lib/stories.ts");
  await assert.rejects(readDraft(new Response("", { status: 401 }), "story"), /signed out/);
  await assert.rejects(readDraft(new Response("", { status: 403 }), "story"), /another artist/);
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 401 }));
  await assert.rejects(getStudio(), /signed out/);
  await assert.rejects(uploadStory(new FormData()), /signed out/);
});
