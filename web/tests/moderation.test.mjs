import test from "node:test";
import assert from "node:assert/strict";
import { getReports, moderate, reasonLabel, REPORT_REASONS, reportStory } from "../lib/moderation.ts";

test("ReportStory_ListedReason_PostsReasonAndTrimmedNote", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => {
    assert.equal(url, "/api/feed/s%2F1/reports"); assert.deepEqual(JSON.parse(options.body), { reason: "Harmful", note: "Hateful" });
    return new Response(null, { status: 202 });
  });
  await reportStory("s/1", "Harmful", "  Hateful  ");
  t.mock.method(globalThis, "fetch", async (url, options) => { assert.equal(JSON.parse(options.body).note, null); return new Response(null, { status: 202 }); });
  await reportStory("s", "Other", "   ");
});
test("ReportStory_InvalidInputOrFailure_Explains", async (t) => {
  await assert.rejects(reportStory("s", "Boring", ""), /Choose a reason/);
  await assert.rejects(reportStory("s", "Other", "n".repeat(501)), /500 characters/);
  for (const [status, message] of [[404, /no longer in the feed/], [429, /Too many/], [500, /could not be sent/]]) {
    t.mock.method(globalThis, "fetch", async () => new Response("", { status }));
    await assert.rejects(reportStory("s", "Other", ""), message);
  }
});
test("GetReports_ModeratorOrNot_ReturnsListOrExplains", async (t) => {
  const item = { storyId: "s", title: "T", artistName: "A", hiddenForReview: false, openReports: 2, reasons: ["Harmful"], notes: [], latestReportAt: "2026-11-11T00:00:00Z" };
  t.mock.method(globalThis, "fetch", async (url, options) => { assert.equal(url, "/api/moderation/reports"); assert.equal(options.cache, "no-store"); return Response.json([item]); });
  assert.equal((await getReports())[0].openReports, 2);
  for (const [status, message] of [[403, /Only Mazwi moderators/], [401, /Sign in/], [500, /could not be loaded/]]) {
    t.mock.method(globalThis, "fetch", async () => new Response("", { status }));
    await assert.rejects(getReports(), message);
  }
  t.mock.method(globalThis, "fetch", async () => Response.json([{ ...item, openReports: "2" }]));
  await assert.rejects(getReports(), /unexpected reports/);
});
test("Moderate_Action_PostsToStoryAction", async (t) => {
  t.mock.method(globalThis, "fetch", async (url, options) => { assert.equal(url, "/api/moderation/stories/s/hide"); assert.equal(options.method, "POST"); return new Response(null, { status: 204 }); });
  await moderate("s", "hide");
  t.mock.method(globalThis, "fetch", async () => new Response("", { status: 404 }));
  await assert.rejects(moderate("s", "restore"), /no longer exists/);
});
test("ReasonLabel_KnownAndUnknown", () => {
  assert.equal(reasonLabel("NoPermission"), "Shared without permission");
  assert.equal(reasonLabel("Unknown"), "Unknown");
  assert.equal(REPORT_REASONS.length, 4);
});
