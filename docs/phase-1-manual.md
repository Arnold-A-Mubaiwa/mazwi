# Mazwi: manual first-phase demo

AI translation drafts are now available as an opt-in on current main; the `phase-1-manual` tag remains the original manual release. See docs/ai-translation-drafts.md. Opt-in AI voices for translations are described in docs/ai-voice.md. Original transcripts still need manual entry/correction and approval.

The approved manual first phase is complete. It works without OpenAI credit.
Release tag: `phase-1-manual`. The original AI-powered `level-1` is not claimed complete.

## Start and stop on Windows

From `C:\noneRepos\mazwi` in PowerShell:

```powershell
./scripts/Start-Mazwi.ps1
```

This loads ignored `.env`, builds the .NET API and Next.js website, applies versioned migrations, starts hidden local services, and verifies a database-backed request through the website.
It requires the pinned .NET SDK, Node.js, installed frontend dependencies and a configured local PostgreSQL connection. It does not require an OpenAI key.
Open `http://127.0.0.1:3000` or `http://127.0.0.1:3000/studio`.
For unchanged existing builds:

```powershell
./scripts/Start-Mazwi.ps1 -SkipBuild
```

To stop only the services started by this helper:

```powershell
./scripts/Stop-Mazwi.ps1
```

The helper refuses unknown port owners and checks process IDs, start times and executable paths before stopping. Existing manual terminals are left alone; stop those with Ctrl+C before first using the helper.
Starting an already-ready managed instance keeps it running. Stop first to rebuild changed code or reload changed settings.
Logs and process state remain under ignored `.local/run/`; secrets are never printed by the helpers.
Stop the app before running builds or the quality/browser checks, to avoid locked files and occupied test ports.

## The demo journey

1. Upload a permitted original audio/video recording at `/upload` (maximum 25 MB).
2. Open **Studio** to find it again, or follow the confirmation's editing link.
3. Listen and save the original transcript and your manual translations.
4. Optionally upload a permitted recording for each translation.
5. Explicitly approve the original, then each translation.
6. Watch it on the home feed or story page; choose a language once.
7. If no translated recording exists, Mazwi labels original audio alongside approved translated text. If the language is not approved, it labels the original-language fallback.
8. Edit approved content and confirm it needs approval again. Source edits remove the story from viewer routes until reapproved.

Studio lists local stories in pages, with a filter for the current page and edit/view links. It is not an account system and does not delete recordings.
Artist/community/country credit is visible. Next/previous, focused arrow keys and vertical swipe navigation are supported. Native audio/video controls start playback; changing stories stops the previous player.

## Completed scope and remaining work

| Working now | Still pending |
| --- | --- |
| Private upload, PostgreSQL metadata, local studio | Automatic transcription and language-quality evaluation |
| Manual transcript and translation editing | AI translation and generated voices (API credit required) |
| Artist-provided translated recordings | ffmpeg dubbing/export and timed subtitles |
| Explicit approval, stale-review protection and revocation | Accounts and hosted access control |
| Approved feed/story playback, saved language preference, credits | Story Twins, statistics, voice stamps/cloning, PWA sharing and Android bubble |

This is a localhost demo. Both application and tests temporarily use the owner's approved shared test database; unpublished synthetic fixture metadata may appear in Studio. Separate databases before hosting.
Browser test files stay separate from real artist files. Tests delete or revoke synthetic approvals, so they do not publish fabricated demo content.
Actual artist content, permission, transcription accuracy and translations still need human review. Real recordings are never published automatically by setup or tests.
Replaced recordings can remain privately stored for later cleanup. Concurrent text edits are last-write-wins per field. Already downloaded audio cannot be recalled.

## Verified release checks

- 183 .NET tests across all four layers, including real local PostgreSQL/filesystem and endpoint tests.
- Zero-warning .NET build, format verification and architecture checks.
- 47 offline Node tests, frontend lint/type-check and production build.
- 22 Edge browser tests, including upload → Studio → edit → translated recording → approve → actual playback → revoke.
- Desktop/mobile screenshots visually reviewed; no horizontal overflow.
- Actual full startup/build/migration, fresh skip-build startup, idempotent startup, safe stop, stale-process rejection and unmanaged-port refusal passed.

Detailed workflows: docs/story-upload.md, docs/manual-story-editor.md, docs/artist-approval.md and docs/viewer-feed.md.
