# Play statistics

Issue #36 shows each artist how often their approved stories are played, without tracking viewers.

## What counts

- A play is counted once a viewer has actually listened or watched for 3 seconds. Seeking forward, pausing and rewinding do not add time.
- A story card counts once, whichever player or toggle the viewer uses.
- Only approved, current versions count. Plays of unpublished stories, unknown stories or a version that changed since the page loaded are ignored (404).
- A play is credited to the language the viewer read. A viewer who chose isiXhosa but was shown the approved Shona original counts as Shona.

## What is stored

One row per story, language and UTC day, holding a number (migration 010, `story_play_counts`). No IP address, cookie, account, device or time of day is stored. Deleting a story deletes its counts.

## Seeing them

Studio shows a line on each published story, such as "12 plays · 3 this week · isiXhosa 9 · Shona 3". "This week" means today and the six days before (UTC). Only the story's owner sees these counts.

| Endpoint | Purpose |
| --- | --- |
| `POST /api/feed/{id}/plays` | Public, `{ language, version }`; 204 counted, 404 not countable, 429 over the per-story limit |
| `GET /api/studio/stats` | Signed-in artist only: totals per story and language for their own stories |

The play endpoint allows 120 plays per story per minute (`Mazwi:PlaysPerStoryPerMinute`), so one busy story cannot block the rest.

## Tests

- **Domain:** which plays count, language crediting, and total invariants.
- **Application:** the UTC day, the seven-day window and grouping.
- **Repository (real PostgreSQL):** increments, owner-only totals and cascade on delete.
- **Endpoints:** 204/404/400/429, owner-only stats, and a real database round-trip with two artists.
- **Web:** tracker thresholds and seeks, the request shape, response validation and summary wording.
- **Browser:** `e2e/plays.spec.ts` plays an approved story for 3 seconds and sees "1 play · 1 this week" in Studio.

## Limits

- Counts can be inflated by someone scripting requests, up to the per-story limit. They are an encouragement for artists, not an audited metric.
- Behind the current Next.js proxy all viewers look the same to the API, so there is no per-viewer limit, by design (see docs/accounts.md).
