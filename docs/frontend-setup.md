# Frontend setup and API connection

Issue #9 adds a local Next.js App Router / TypeScript / Tailwind shell in `web/`.
Business logic, AI calls, SQL, and persistence remain in the .NET backend layers.
The manual demo includes uploads, Studio, manual text, recording playback, explicit artist approval and the viewer feed. AI processing and accounts remain pending. See docs/phase-1-manual.md.

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
The production build must exist before the browser suite runs. First load the ignored environment with scripts/Set-LocalEnvironment.ps1 and run the .NET tests to migrate the dedicated test database. Browser uploads use MAZWI_TEST_CONNECTION_STRING and private .local/browser-media storage. The suite starts and stops its own .NET and production Next.js servers on ports 5046 and 3000; stop any other processes you own on those ports first.
Browser tests use the installed Microsoft Edge channel, not a downloaded browser.
The test checks actual API responses through the Next.js proxy, navigation, persisted preferences, mobile/desktop layout, and an unavailable-API response.
Generated `.next/`, browser artifacts, TypeScript build metadata, and `node_modules/` are ignored by Git.

## Run locally

Recommended from the repository root: `./scripts/Start-Mazwi.ps1`. Stop with `./scripts/Stop-Mazwi.ps1`. These verified Windows helpers load local settings, build/migrate, check database readiness, and protect unrelated processes. See docs/phase-1-manual.md. The two-terminal alternative below remains available.

After building the .NET solution and frontend, use two PowerShell terminals in the repository root.
In the API terminal, load the ignored local settings first. A normal `dotnet run` does not read `.env` automatically; skipping this step can leave the website reachable while database-backed uploads fail.

```powershell
. ./scripts/Set-LocalEnvironment.ps1
dotnet run --project src/Mazwi.WebApi --no-launch-profile --no-build --urls http://127.0.0.1:5046
```

```powershell
npm.cmd --prefix web run start -- --port 3000
```

Open `http://127.0.0.1:3000`. The API is `http://127.0.0.1:5046`.
`/status` is a separate local-development connection check; it is not part of the viewer navigation.

Next.js forwards `/api/*` to the API. `MAZWI_API_URL` defaults to `http://127.0.0.1:5046` and may be set privately in `web/.env.local` before building.
Do not put OpenAI keys, database credentials, SQL, or business rules in frontend code or `NEXT_PUBLIC_*` variables.
`GET /api/health` reports API liveness only. It does not claim that PostgreSQL, OpenAI, or media processing are ready.

## UI and feature boundaries

The UI uses a dark earth palette, ochre and red accents, large headings, and a phone-first layout.
The language preference is saved locally; choosing a language does not claim that generated audio has passed review.
Only artist-approved content enters viewer routes. An empty feed stays empty; setup does not fabricate or publish artist content.
`web/lib/config.ts` holds `FEATURES`; local uploads and manual editing are enabled, while AI processing and later features remain disabled. Client flags are display controls, not authorization.

The .NET build and format checks pass, and the health test runs without database credentials.
The PostgreSQL-dependent suite now passes with the configured local test database. Missing credentials still fail explicitly; never silently skip these tests.
