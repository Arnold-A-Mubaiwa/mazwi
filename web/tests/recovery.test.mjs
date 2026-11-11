import test from "node:test";
import assert from "node:assert/strict";
import { requestPasswordReset, resendVerification, resetPassword, verifyEmail } from "../lib/recovery.ts";
import { getCurrentArtist } from "../lib/account.ts";

test("RequestPasswordReset_ValidEmail_PostsTrimmedEmail", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/account/password-reset"); assert.deepEqual(JSON.parse(options.body), { email: "rudo@example.com" });
    return new Response(null, { status: 202 });
  });
  await requestPasswordReset(" rudo@example.com ");
});
test("RequestPasswordReset_InvalidEmailOrLimited_Explains", async (t) => {
  await assert.rejects(requestPasswordReset("rudo"), /valid email/);
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 429 }));
  await assert.rejects(requestPasswordReset("rudo@example.com"), /Too many/);
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 500 }));
  await assert.rejects(requestPasswordReset("rudo@example.com"), /couldn.t send/);
});
test("ResetPassword_ChecksBeforeSending", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => new Response(null, { status: 204 }));
  await assert.rejects(resetPassword("", "long password", "long password"), /incomplete/);
  await assert.rejects(resetPassword("t", "long password", "other password"), /do not match/);
  await assert.rejects(resetPassword("t", "short", "short"), /10.128/);
  assert.equal(request.mock.callCount(), 0);
  await resetPassword("t", "long password", "long password");
  assert.deepEqual(JSON.parse(request.mock.calls[0].arguments[1].body), { token: "t", password: "long password" });
});
test("ResetPasswordAndVerify_BadLink_SaysRequestANewOne", async (t) => {
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 400 }));
  await assert.rejects(resetPassword("t", "long password", "long password"), /Request a new one/);
  await assert.rejects(verifyEmail("t"), /Request a new one/);
  await assert.rejects(verifyEmail(""), /incomplete/);
});
test("VerifyEmail_GoodLink_PostsToken", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => { assert.equal(url, "/api/account/verify-email"); assert.deepEqual(JSON.parse(options.body), { token: "abc" }); return new Response(null, { status: 204 }); });
  await verifyEmail("abc");
});
test("ResendVerification_SignedOutOrLimited_Explains", async (t) => {
  t.mock.method(globalThis, "fetch", async () => new Response(null, { status: 202 }));
  await resendVerification();
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 401 }));
  await assert.rejects(resendVerification(), /Sign in again/);
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 429 }));
  await assert.rejects(resendVerification(), /Too many/);
});
test("GetCurrentArtist_EmailVerifiedFlag_IsRead", async (t) => {
  t.mock.method(globalThis, "fetch", async () => Response.json({ id: "a", email: "rudo@example.com", emailVerified: true }));
  assert.equal((await getCurrentArtist()).emailVerified, true);
});
