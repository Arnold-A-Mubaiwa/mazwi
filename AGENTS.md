# Repository guidance

Keep this file under 100 lines. Record executed commands and outcomes; use TODO for unknowns.
Make changes only within `C:\noneRepos\mazwi\`, except explicitly authorized user-level skills.

## 1. About the project

Mazwi is an African storytelling platform under construction, with a Next.js/TypeScript/Tailwind frontend and a .NET API.
It uses C#/.NET 10 LTS, ASP.NET Core Web API, PostgreSQL, and Dapper via Npgsql; no Entity Framework.

## 2. Install, run, build and test

SDK 10.0.402 is pinned in global.json. Quality tooling also uses Node.js.
- Install: `dotnet restore Mazwi.sln --configfile NuGet.Config`
- Run: `./scripts/Start-Mazwi.ps1` (loads ignored .env, builds, migrates and checks local readiness).
- Stop: `./scripts/Stop-Mazwi.ps1` before builds or quality/browser checks; only helper-managed processes are stopped.
- Build: `dotnet build`
- Lint: `dotnet format --verify-no-changes`
- Test: `dotnet test`
- Architecture: `node .codex/skills/repo-quality/scripts/architecture.mjs`
- Frontend install: `npm.cmd install --prefix web --ignore-scripts --no-audit --no-fund --cache .local/npm-cache --prefer-offline`
- Frontend build: `npm.cmd --prefix web run build`
- Frontend lint: `npm.cmd --prefix web run lint`
- Frontend type-check: `npm.cmd --prefix web run typecheck`
- Frontend tests: `npm.cmd --prefix web test`
- Frontend browser tests: `npm.cmd --prefix web run test:e2e`

Build must have zero warnings; format and all four test projects must pass before a PR is ready.
Repository/full-stack API tests currently require MAZWI_TEST_CONNECTION_STRING for a dedicated local database ending in _tests; never mock the database in Repository.Tests.
Local PostgreSQL is the current user-approved default. The owner temporarily approved sharing the dedicated _tests database with the local app; separate development storage later. Testcontainers/Docker remain optional future work.
Secrets belong in ignored .env/appsettings.Development.json or user-secrets, never tracked files.
The quality helper loads .env and uses repository-local .local caches. Missing credentials are failures, not skipped/passing tests.

## 3. Architecture and coding standards

Dependencies point inward only:
- Mazwi.Domain: entities, immutable record value objects, aggregates, domain services/exceptions, repository interfaces. No project/package references, Dapper, SQL, or ASP.NET.
- Mazwi.Application: use cases, DTOs, orchestration; references Domain only. Database access only through Domain repository interfaces.
- Mazwi.Repository: Dapper/Npgsql implementations, SQL, versioned DbUp migrations; references Domain only.
- Mazwi.Ai: external AI/media adapters; references Domain only, no packages or SQL. Use HttpClient/System.Text.Json, cancellation and safe errors.
- Mazwi.WebApi: thin controllers, request/response models, DI composition root; references Application, Repository and Ai only. Validate input, call one app service, return its result; no business rules or SQL.
- Business rules live in rich Domain entities/value objects, not app services or controllers. No anemic models.
- Encapsulate state with private setters or getters; mutate only through invariant-enforcing methods. Constructors/factories reject invalid state. Entities have identity; value objects are immutable records.
- Program to interfaces, inject dependencies through constructors, follow SOLID; one class has one responsibility.
- Standard C# and LINQ only. No MediatR, AutoMapper, or magic libraries without planning approval.
- Prefer readable code, small methods, meaningful names; avoid premature abstraction.
- Use async/await throughout I/O and pass CancellationToken. DbUp migration execution is synchronous; TODO: obtain approval for the explicit migration-command-only exception. Request handlers remain fully async.
- Frontend: web/ uses strict TypeScript, App Router and Tailwind, two-space indentation and double quotes. Keep business rules, SQL and API keys out of it.
- Parameterized SQL only; never concatenate/interpolate SQL. SQL lives only in Repository.
- Schema changes use versioned migration scripts, never manual schema edits. PostgreSQL tables/columns use snake_case.
- Follow .editorconfig: four-space C#, file-scoped namespaces, meaningful names; nullable, implicit usings, warnings-as-errors via Directory.Build.props.
- Every change ships with tests covering happy paths, boundary/edge cases, invalid input, and failures.
- Mazwi.Domain.Tests: xUnit tests for every entity, value object, and invariant.
- Mazwi.Application.Tests: each app service method; mock repositories with NSubstitute.
- Mazwi.Repository.Tests: integration tests against real PostgreSQL; no database mocks. Local PostgreSQL is the current approved exception to Testcontainers execution.
- Mazwi.WebApi.Tests: WebApplicationFactory endpoint tests for success, validation, not-found, and errors; include a real database round-trip test.
- Test names: Method_Scenario_ExpectedResult. Arrange / Act / Assert.

## 4. Git rules

Use main in this repository; the owner approved it instead of master.
Git commands below are required workflow templates, not claims of project validation.
On Windows PowerShell without &&, run commands separately and stop on failure.

### Plan and issue
- PLAN TOGETHER: investigate read-only, then discuss the problem, root cause if known, change, files per layer, migrations, acceptance criteria, tests per layer, and risks. No issue, branch, or code yet.
- WAIT FOR MY DECISION: only explicit approval such as go / approved / take it on counts. If no, stop and create nothing.
- CREATE THE ISSUE after approval: check duplicates with `gh issue list --search "<keywords>"`; reuse a matching issue, otherwise `gh issue create --title "<summary>" --body "<agreed plan>"`.
- FINISH WITHOUT ASKING: branch, implement, tests, lint, push, draft PR. Stop only if the plan is wrong, scope grows meaningfully, or something is blocked.
- No approved plan = no issue. No issue number = no code. If gh is unavailable or this is not GitHub, stop and ask.

### Branching
- Always branch from updated main: `git switch main && git pull && git switch -c <branch>`.
- Never branch from whatever is checked out or from uat. One branch holds one piece of work.
- Branch names: `<type>/<issue-number>-<short-slug>`.

### Upstream tracking
- A working branch must never track main.
- First push: `git push -u origin <branch>`.
- Verify: `git rev-parse --abbrev-ref '<branch>@{upstream}'` must print origin/<branch>, never origin/main.
- Wrong upstream: `git branch --unset-upstream <branch>`, push again with -u, then verify.
- Worktrees always use --no-track: `git worktree add --no-track <path> -b <branch> origin/main`.

### Commits, PRs and UAT
- Never commit/push directly to main or merge into main locally. Every commit includes Refs #<issue-number>.
- Before a PR: dotnet build and dotnet test must pass with zero warnings; run dotnet format --verify-no-changes and architecture checks. The initial scaffold draft documents deferred database validation; never mark it ready until that passes.
- Open a draft PR targeting main with Summary / Tests / Ticket and Closes #<issue-number>.
- After all checks pass, the standing user preference permits marking ready and enabling auto-merge; respect GitHub checks/reviews. Never bypass protections.
- If uat exists, merge the working branch into uat to test; fix on the working branch and merge it into uat again. Never merge uat into main.

## 5. Documentation

For OpenAI API, Apps SDK, or Codex work, use openaiDeveloperDocs MCP without being asked.
The canonical repo-quality skill is .codex/skills/repo-quality/SKILL.md; .agents/skills/repo-quality/SKILL.md provides discovery.
