import test from "node:test";
import assert from "node:assert/strict";
import { resolveLanguage, LANGUAGES } from "../lib/languages.ts";
import { getApiHealth } from "../lib/api.ts";
test("ResolveLanguage_KnownCodes_PreservesPreference", () => {
  for (const language of LANGUAGES) assert.equal(resolveLanguage(language.code), language.code);
});
test("ResolveLanguage_InvalidStoredValue_FallsBackToShona", () => {
  for (const value of [null, undefined, "", "unknown", {}, 42]) assert.equal(resolveLanguage(value), "sn");
});
test("GetApiHealth_ValidResponse_PassesCancellationAndAvoidsCache", async (t) => {
  const controller = new AbortController();
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/health");
    assert.equal(options.signal, controller.signal);
    assert.equal(options.cache, "no-store");
    return Response.json({ status: "ok", service: "Mazwi" });
  });
  assert.deepEqual(await getApiHealth(controller.signal), { status: "ok", service: "Mazwi" });
});
test("GetApiHealth_ErrorOrWrongService_Rejects", async (t) => {
  const request = t.mock.method(globalThis, "fetch", async () => new Response("", { status: 503 }));
  await assert.rejects(getApiHealth(), /unavailable/);
  request.mock.mockImplementation(async () => Response.json({ status: "ok", service: "other" }));
  await assert.rejects(getApiHealth(), /unexpected/);
});
