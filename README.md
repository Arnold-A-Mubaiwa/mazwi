# mazwi

Mazwi: Every African story, in your language. A Next.js frontend and .NET 10 LTS API with inward DDD dependencies and PostgreSQL.

The [manual first-phase demo](docs/phase-1-manual.md) is complete: upload, local Studio, manual transcripts/translations, artist-provided recordings, explicit approval and approved viewer playback. It works without OpenAI credit. [AI translation drafts](docs/ai-translation-drafts.md) and [AI voices](docs/ai-voice.md) are now available behind opt-in flags; transcription and later levels remain pending. The task-item API below remains the original architecture example.

From this repository's root in PowerShell, run `./scripts/Start-Mazwi.ps1`, then open `http://127.0.0.1:3000`. Stop with `./scripts/Stop-Mazwi.ps1`. See the manual-demo guide for setup, verification, boundaries and the `phase-1-manual` tag.

## Structure

- Domain: rich story/text/recording/approval rules, immutable value objects and repository interfaces; includes the original task example.
- Application: upload, editing, review, feed and studio use cases/DTOs; depends only on Domain.
- Repository: parameterized Dapper queries, Npgsql connections, embedded versioned DbUp scripts; depends only on Domain.
- Ai: external AI adapters using HttpClient/System.Text.Json, no packages; depends only on Domain.
- WebApi: thin controllers and DI composition; references Application, Repository and Ai.
- Four xUnit projects cover domain, application (NSubstitute), real PostgreSQL persistence, and HTTP endpoints (WebApplicationFactory).

## Local development

Install the SDK pinned in global.json. PostgreSQL 18 is the current local target.
Normally use separate development and test databases; the test database name must end in `_tests`. The owner has temporarily approved using that test database for the local demo too. Both private environment settings point to it; application uploads will coexist with test records until development storage is separated.
The tests migrate that test database and insert uniquely identified records; they never truncate it.

Keep credentials in an ignored `.env`, ignored `src/Mazwi.WebApi/appsettings.Development.json`, process environment, or ASP.NET user-secrets (ID: `mazwi-webapi-development`).
`.env.example` lists the required names without credentials or connection strings.
`ConnectionStrings__Mazwi` configures the API; `MAZWI_TEST_CONNECTION_STRING` configures integration tests.
The quality helper reads `.env`; the API uses normal ASP.NET configuration and does not automatically load `.env`.

Run the quality workflow from the repository root:

```text
node .codex/skills/repo-quality/scripts/check.mjs
```

The helper uses repository-local .NET/NuGet caches under ignored `.local/`.
For direct .NET commands, `scripts/Set-LocalEnvironment.ps1` sets these environment variables and loads `.env` without echoing secrets; dot-source it in a PowerShell session that permits local scripts.

```text
dotnet restore Mazwi.sln --configfile NuGet.Config
dotnet build
dotnet format --verify-no-changes
dotnet test
```

After configuring the API connection string, run the WebApi project with `--migrate` as an application argument to apply DbUp migrations, then start it normally.
Migrations are explicit, transactional, and journaled; no database schema changes run automatically when the API starts.
DbUp's migration execution is synchronous; approval for that migration-only exception is pending. All request-path database I/O is asynchronous and cancellable.

Endpoints: POST `/api/task-items` with a title, and GET `/api/task-items/{id}`.
A title is trimmed and must contain 1–200 characters. Errors use Problem Details without leaking infrastructure exception details.

Local PostgreSQL is the current approved default for both application and integration tests.
The requested Testcontainers.PostgreSql dependency is retained for future container execution but is not used in local mode.
`docker-compose.yml` provides an optional `docker` profile on port 5433, with its password supplied through the environment; Docker is not required for the current workflow.

## Validation limits

Missing database credentials cause integration tests to fail explicitly, never to be skipped.
Endpoint contract tests use a substituted repository, while a separate PostgreSQL endpoint test exercises the real API-to-database wiring.
The architecture checker enforces the production project graph and package allowlist and scans SQL placement/construction; semantic DDD review remains necessary.
