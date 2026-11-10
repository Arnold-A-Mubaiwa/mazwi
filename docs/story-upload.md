# Local story uploads

Issue #11 adds `/upload`, `POST /api/stories` (multipart), and `GET /api/stories/{id}` (metadata).
This is an unauthenticated local demo; keep the servers bound to localhost. Do not expose it as a hosted multi-user service.

## What works

- Recordings: MP3, WAV, M4A, MP4 and WebM; non-empty and at most 25,000,000 bytes.
- Title (1–200 characters), artist and community (1–100), two-letter country code, type (story/poem/folktale/song), language (sn/xh/sw/en).
- Required permission to store/process the recording, with a server-recorded timestamp. This permission does not authorize voice cloning.
- Trimmed metadata and an unpublished story stored in PostgreSQL through Dapper.
- Private media in the repository's ignored `.local/media/`, with generated identity-based filenames. Client filenames cannot select a destination.
- Basic file-header checks; these do not prove complete media decodability. Actual decoding/transcoding belongs to the later processing feature.
- An unpublished confirmation linking to manual transcript/translation editing, without claiming AI processing or approval happened.

Uploads are not served through a public static folder. Later completed issues add local artist review/player routes and approval-checked viewer playback; see docs/artist-approval.md and docs/viewer-feed.md. No AI calls or accounts are added. Manual editing is documented in docs/manual-story-editor.md.
The metadata lookup is also unauthenticated and is for local development only.

## Database and configuration

Set `ConnectionStrings__Mazwi` privately. The owner currently permits the local app to share the `_tests` database: both it and `MAZWI_TEST_CONNECTION_STRING` point to the same database in ignored `.env`. Uploaded demo stories and test records therefore coexist. Split development and test databases later.
Use `.env` with `scripts/Set-LocalEnvironment.ps1`, ignored appsettings.Development.json, or user-secrets; never commit connection strings.
Migration `002_create_stories.sql` is embedded in Repository and runs through the existing explicit DbUp migration command, not normal API startup.
Recordings are not stored in `web/public/`. `MediaStorage__Root` can override the private filesystem location; never point it at a public/static folder.

After setting the development connection in the ignored `.env`, run from the repository root:

```powershell
. ./scripts/Set-LocalEnvironment.ps1
dotnet build
dotnet run --project src/Mazwi.WebApi --no-launch-profile --no-build -- --migrate
```

The explicit migration command was verified against the configured local database. It uses `ConnectionStrings__Mazwi`, which currently points to the shared test database under the owner's temporary exception.
Then start the API and website using docs/frontend-setup.md (load the local environment in the API terminal first), and open `http://127.0.0.1:3000/upload`.

## Failure behavior

Input validation, size limits, permission, supported types, and file-header checks run on the server. Browser checks are only convenience feedback.
The filesystem implementation stages each file, bounds the bytes copied, rejects incomplete uploads, cleans temporary files, and never overwrites an existing recording.
If database persistence fails, the application attempts to remove the saved recording even after request cancellation.
Filesystem and database operations are not one transaction: a process crash or failed cleanup can leave an orphan file. Production storage/reconciliation is future work.
An uncertain network result may have saved a story; do not repeatedly submit blindly. Idempotent uploads are future work.

## Verification

The standard quality helper runs the .NET build, format, all four test projects and architecture checks.
Frontend checks are documented in docs/frontend-setup.md.
Browser tests use the explicit test connection and `.local/browser-media/`, not real artist recordings or the app's `.local/media/` files. Their metadata shares the current database under the temporary exception above.
They verify a real browser-to-API-to-PostgreSQL upload, private file contents, a file larger than 10 MB, permission validation, failure recovery and mobile layout.
Run .NET tests first to apply the migration to the dedicated test database, then run the frontend production build and browser suite.

Verified for this feature: 106 .NET tests, 32 offline Node tests (including 9 frontend unit tests), 10 browser tests; .NET build (zero warnings), format, architecture, frontend lint/type-check and production build all passed.
