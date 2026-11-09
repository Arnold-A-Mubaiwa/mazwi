# Frontend setup and API connection

Issue #9 adds a local Next.js App Router / TypeScript / Tailwind shell in `web/`.
Business logic, AI calls, SQL, and persistence remain in the .NET backend layers.
No upload, translation, feed media, or authentication workflow is implemented here.

## Install and validate (repository root)

```powershell
npm.cmd install --prefix web --ignore-scripts --no-audit --no-fund --cache .local/npm-cache --prefer-offline
npm.cmd --prefix web run lint
npm.cmd --prefix web run typecheck
npm.cmd --prefix web test
npm.cmd --prefix web run build
npm.cmd --prefix web run test:e2e
```

The dependency manifest and lock file pin the installed versions.
The production build must exist before the browser suite runs. The suite starts and stops its own .NET and production Next.js servers on ports 5046 and 3000; stop any other processes you own on those ports first.
Browser tests use the installed Microsoft Edge channel, not a downloaded browser.
The test checks actual API responses through the Next.js proxy, navigation, persisted preferences, mobile/desktop layout, and an unavailable-API response.
Generated `.next/`, browser artifacts, TypeScript build metadata, and `node_modules/` are ignored by Git.

## Run locally

After building the .NET solution and frontend, use two terminals whose working directory is `web/`:

```powershell
dotnet run --project ../src/Mazwi.WebApi --no-launch-profile --no-build --urls http://127.0.0.1:5046
```

```powershell
npm.cmd run start -- --port 3000
```

Open `http://127.0.0.1:3000`. The API is `http://127.0.0.1:5046`.
`/status` is a separate local-development connection check; it is not part of the viewer navigation.

Next.js forwards `/api/*` to the API. `MAZWI_API_URL` defaults to `http://127.0.0.1:5046` and may be set privately in `web/.env.local` before building.
Do not put OpenAI keys, database credentials, SQL, or business rules in frontend code or `NEXT_PUBLIC_*` variables.
`GET /api/health` reports API liveness only. It does not claim that PostgreSQL, OpenAI, or media processing are ready.

## UI and feature boundaries

The UI uses a dark earth palette, ochre and red accents, large headings, and a phone-first layout.
The language preference is saved locally; choosing a language does not claim that generated audio has passed review.
Until stories are implemented, the viewer sees an honest empty state instead of fake approved recordings.
`web/lib/config.ts` holds `FEATURES`; upload and all future-level features remain disabled. Client flags are display controls, not authorization.

The .NET build and format checks pass, and the health test runs without database credentials.
The existing full .NET suite still has 9 PostgreSQL-dependent failures until `MAZWI_TEST_CONNECTION_STRING` is configured. Do not report those checks as passed or silently skip them.
