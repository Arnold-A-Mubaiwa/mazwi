---
name: repo-quality
description: Build, format-check, test, and inspect DDD dependencies in the Mazwi .NET solution. Use for quality checks, failing checks, or PR readiness reviews; propose minimal fixes for approval.
---

# Repository quality

Read AGENTS.md and the referenced scripts before executing checks.

1. Run `dotnet build` (zero warnings), `dotnet format --verify-no-changes`,
   and `dotnet test` across all four C# test projects using AGENTS.md commands.
   Run the architecture helper to check inward references, forbidden Domain
   infrastructure dependencies, and SQL outside Repository. Also review changed
   code for business logic placement, parameterization, and CancellationToken.
2. If tests do not exist, establish minimal xUnit tests for the existing layers,
   including one real smoke test. Follow AGENTS.md plan/issue/branch gates before
   writing. Never replace real PostgreSQL tests with mocks or silent skips.
   Local PostgreSQL is currently approved; use MAZWI_TEST_CONNECTION_STRING for
   a dedicated database ending in _tests. Do not install or bootstrap during a
   check-only invocation without the required approval.
3. Run `node .codex/skills/repo-quality/scripts/check.mjs` from the root.
   It reads Build, Lint, Test, and Architecture commands from AGENTS.md section 2,
   loads an ignored .env file without shell evaluation, and runs each check
   independently. Missing, duplicate, or TODO commands are BLOCKED. If formatting
   differs, read and run commands manually rather than guessing. Inspect scripts
   first; stop for approval if a check repairs code or installs dependencies.
4. Group failures by likely root cause with evidence and affected files.
   Separate environment blockers from defects. Propose minimal fixes, but
   do not apply them until the user approves. A failing test does not authorize
   application changes or framework replacement.
5. Finish with command/status/exit-code rows for build, format, tests, and
   architecture, then a short PASS/FAIL report. PASS requires all four checks
   to pass; BLOCKED means FAIL (incomplete). State coverage limits and pending
   approval items. Do not claim every possible case is covered.
