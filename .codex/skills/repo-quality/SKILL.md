---
name: repo-quality
description: Check this repository's lint, type-check, and tests, group failures by likely root cause, and propose minimal fixes for approval. Use for repository quality checks, failing checks, or readiness reviews.
---

# Repository quality

Read root AGENTS.md before executing checks or changing files. Follow its issue,
branch, commit, and PR rules for any bootstrap work. Do not switch branches or
create issues for a check-only invocation.

1. Read the Install, run, build and test section in AGENTS.md. Use its Lint,
   Type-check, and Test commands verbatim from the repository root. Inspect the
   referenced scripts first; stop for approval if a documented check uses
   automatic repair flags, would modify source, or would install dependencies.
2. If there is no test suite, first set up the smallest framework supported by
   the existing stack and add one smoke test of a real repository invariant.
   For this repository's Node tooling, use the built-in node:test runner without
   dependencies. Initial framework setup and its smoke test are authorized by
   invocation; existing failures and application changes are not. Follow Git
   rules before writing. Run the test and only then document its command in
   AGENTS.md. If no runtime is available, report BLOCKED rather than guessing.
3. Run the helper with `node .codex/skills/repo-quality/scripts/check.mjs`.
   It reads single-line backtick commands labeled Lint, Type-check, and Test in
   section 2 of AGENTS.md. TODO, missing, duplicate, or unsupported declarations
   are BLOCKED. If formatting differs, read and run the commands yourself;
   never silently invent a replacement. Run independent checks even after one
   fails. The helper only runs commands; it does not install, bootstrap, or fix.
4. Read output and group failures by likely root cause, not one bullet per error.
   Cite affected files and evidence, distinguish hypotheses, and separate
   missing tooling from defects. Propose the smallest fix for each group.
   Do not apply proposed fixes until the user explicitly approves them. Do not
   bootstrap again merely because an existing smoke test fails. Approval to
   create or invoke this skill does not authorize fixing observed failures.
5. Finish with command/status/exit-code rows for all three checks and a short
   PASS/FAIL report. Overall PASS requires all three checks to pass; a BLOCKED
   check makes the overall result FAIL (incomplete), never a clean pass.
   State smoke-test coverage limits and list pending approval items. If the user
   asks for a PR, use a draft PR and disclose any missing checks.
