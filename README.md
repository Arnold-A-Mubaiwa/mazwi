# mazwi

Mazwi: Every African story, in your language. A Next.js frontend and .NET 10 LTS API with inward DDD dependencies and PostgreSQL.

Local story upload is available at `/upload`: save a consented recording privately and keep its metadata unpublished. See [story upload](docs/story-upload.md). AI processing and the approved-story feed are still pending. The task-item API below remains the original architecture example.

## Structure

- Domain: task aggregate, immutable title value object, invariant, repository interface.
- Application: task use cases and DTOs; depends only on Domain.
- Repository: parameterized Dapper queries, Npgsql connections, embedded versioned DbUp scripts; depends only on Domain.
- WebApi: thin controllers and DI composition; references Application and Repository.
- Four xUnit projects cover domain, application (NSubstitute), real PostgreSQL persistence, and HTTP endpoints (WebApplicationFactory).

## Local development

Install the SDK pinned in global.json. PostgreSQL 18 is the current local target.
Use separate development and test databases; the test database name must end in `_tests`.
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
