# Reporting and moderation

Issue #49 lets viewers flag a problem with a published story and lets Mazwi moderators act on it.

## Reporting a story

Every story card in the feed has **Report this story**. The viewer chooses a reason and can add a note of up to 500 characters:

| Reason | Stored as |
| --- | --- |
| Shared without permission | `NoPermission` |
| Hateful, violent or harmful | `Harmful` |
| Wrong translation or misleading label | `Misleading` |
| Something else | `Other` |

- Reports are anonymous: Mazwi stores the story, reason, note and time, but not who sent the report (no account, address or cookie).
- Each visitor address can send 10 reports a minute (`Mazwi:ReportsPerMinute`). When hosted behind the trusted proxy, that is the real visitor's address.
- Only stories currently in the feed can be reported.

## Moderators

Moderators are the accounts whose email is listed in `Mazwi:Moderators` (comma-separated) **and confirmed**. A listed address only grants moderation after its owner opens the confirmation link, so nobody becomes a moderator by signing up with an address they cannot read.

Moderators see **Moderation** in the header. `/moderation` lists stories with open reports, newest first, showing the number of reports, the reasons and recent notes. For each story a moderator can:

| Action | Effect |
| --- | --- |
| **Hide for review** | Leaves the feed at once (no playback, no plays counted, no new reports). The reports stay open. |
| **Restore to feed** | Back in the feed; its reports are closed |
| **Close reports, no action** / **keep hidden** | Closes the reports and leaves the story as it is |

Hiding does not change the artist's words or approvals. Studio and the editor show "Hidden for review", and approving again does not bring the story back until a moderator restores it.

| Endpoint | Purpose |
| --- | --- |
| `POST /api/feed/{id}/reports` | Public, `{ reason, note? }`; 202, 400 unknown reason, 404 not in feed, 429 limit |
| `GET /api/moderation/reports` | Moderators only: open reports grouped by story |
| `POST /api/moderation/stories/{id}/hide` · `/restore` · `/resolve` | Moderators only; 204 or 404 |

`GET /api/account/me` includes `isModerator`. Migration 012 adds `stories.hidden_for_review` and `story_reports`.

## Tests

- **Domain:** hiding removes the story from the feed and from plays and reports; restore; report rules (reasons, note length, control characters, only published stories).
- **Application:** filing reports; hide, restore and resolve; missing stories.
- **Repository (real PostgreSQL):** grouping with reasons and notes; hidden stories leave the feed listing; resolving.
- **Endpoints:** report status codes and limits; moderators allowed; an unlisted or unconfirmed account gets 403; signed out gets 401.
- **Browser:** `e2e/moderation.spec.ts`:
  1. an anonymous viewer reports;
  2. the fixture artist gets 403 on the moderation API;
  3. the moderator (the second fixture artist, confirmed through the outbox by `auth.setup.ts`) sees the report and hides the story;
  4. the feed shows 404, and the artist sees "Hidden for review";
  5. the moderator restores it, and the feed works again.

## Limits

- There is no message to the artist explaining why a story was hidden yet; moderators contact them by email.
- Reports cannot be appealed in the app.
- With the local outbox, moderators must confirm their email through `.local/outbox` until a real email provider is added.
