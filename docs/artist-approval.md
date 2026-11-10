# Artist review, recordings and approval

Issue #15 extends the manual editor with original playback, optional artist-provided translated recordings and explicit language approval.

1. Upload a permitted original and open its editing link.
2. Listen, save the original transcript, then choose **Approve original**.
3. Save a translation. Optionally upload a permitted recording in that language (25 MB maximum, same supported formats as original uploads).
4. Listen and choose **Approve translation**. Without a translated recording, the later viewer uses approved text with clearly labelled original audio.

Nothing is approved automatically. Unsaved text disables approval and translated recording upload.
Changing the original transcript revokes every approval. Changing one translation or its recording revokes only that language. Saving identical text preserves approval.
Approval includes the current saved review version. Concurrent changes return 409 and require reloading/reviewing; this prevents approval of stale content.
Approval state and content changes are written transactionally under the story row lock. Reads use a consistent database snapshot.

The original approval makes the story eligible for the next viewer-feed feature; translated versions still need individual approval.
These are unauthenticated local artist routes, not a hosted access-control system. Keep the app bound to localhost. Accounts remain pending.

## Media

`GET /api/stories/{id}/recordings/{language}` streams a local artist recording with byte-range support and no-store caching.
`POST` to the same route uploads a translated recording, requiring saved translation text, explicit permission and the current review version.
`POST /api/stories/{id}/approvals/{language}` accepts `{ "reviewVersion": "..." }`.

Files stay outside public static folders with generated identifiers. No filesystem paths are returned.
An available recording is required for approval. File headers and sizes are checked; the artist must verify actual playback and meaning.
Failed new persistence removes the new media file. Replaced files may remain privately stored for later cleanup; no public route exposes old file identities.
Already delivered audio cannot be recalled by revoking approval; later viewer requests will recheck current approval.
No AI-generated voice or translation is claimed, and no OpenAI request is made.

Migration 004 adds review versions, approval rows and translated recording metadata. Published status is derived from original approval rather than a separate mutable status column.
Tests use generated fixture identities. Approved database fixtures are removed or have their approvals revoked so synthetic approved stories do not enter the future demo feed.

Verified: 171 .NET tests, 41 offline Node tests and 16 Edge browser tests; zero-warning build, format, architecture, frontend lint/types and production build all passed.

## Translations the artist cannot read

Approving a translation records whether the artist speaks that language and checked it (`speakerChecked`, optional in `POST /api/stories/{id}/approvals/{language}`, default false). Migration 007 adds `story_approvals.speaker_checked`; existing translation approvals load as not checked, and the original language always counts as checked. The feed says "Approved by <artist>. Not checked by a <language> speaker." or "Approved by <artist>, who checked the <language>." Editing text or replacing the recording revokes approval, which also clears the flag.
