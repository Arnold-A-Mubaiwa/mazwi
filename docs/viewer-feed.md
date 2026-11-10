# Approved viewer feed

Issue #17 replaces the empty home feed with artist-approved stories and adds `/story/<id>`.
Choose a language once; the preference is shared between the feed and story pages and saved locally.

## Playback rules

- Only stories with an approved original are eligible for viewer routes.
- If the requested translation is approved and has a recording, play that artist-provided recording.
- If its text is approved but no translated recording is available, show approved translated text and explicitly label original-language audio.
- If the requested language is not approved, show only the approved original words/audio and explain the fallback.
- Missing translated files can fall back to original audio, preserving the accurate audio-language label. Stories with no playable approved media are omitted.
- **Hear original** switches to the original recording while keeping the approved translated text.

The text beside the player is not a timed subtitle track. No AI voice, automatic translation, voice cloning or video dubbing is claimed.
Native controls require the viewer to start playback. Changing stories/languages stops the previous player.
Use next/previous buttons, arrow keys while the story card is focused, or vertical swipes. More results are loaded as the viewer reaches the end of a page.
Artist, community, country and approved language credits appear with each story.

## Approval checks

Viewer JSON exposes approved projections only; draft text and translated recording identifiers are not returned.
Every recording request rechecks approval and the saved review version before streaming with byte-range support.
Source changes hide the story from future viewer requests until it is approved again. Stale versions return 409; revoked/missing stories return 404.
Already delivered bytes cannot be recalled. Refresh stories after an artist changes content.
The separate local artist routes can still review drafts; this app has no account-based access control and must remain on localhost.

## Endpoints

- `GET /api/feed?language=sn&offset=0&limit=20`: approved items and the next offset; maximum 50 items per page.
- `GET /api/feed/{id}?language=sn`: one approved story projection.
- The returned `mediaUrl` provides version-checked approved playback; no disk path is exposed.

Filtering, fallback and approval rules live in Domain. Application handles media availability through interfaces. SQL remains parameterized in Repository. Controllers only validate/translate HTTP requests.
Tests use synthetic recordings and revoke/delete synthetic approvals after verification. The current shared local database may retain unpublished fixture metadata.

Verified: 181 .NET tests, 45 offline Node tests and 19 Edge browser tests. Zero-warning build, formatting, architecture, frontend lint/types and production build pass. Desktop/mobile viewer screenshots were visually reviewed.
