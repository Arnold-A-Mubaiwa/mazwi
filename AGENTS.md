# Repository guidance

Keep this file under 100 lines. Record project commands only after running them successfully; use TODO for unknowns.
Make changes only within `C:\noneRepos\mazwi\`.

## 1. About the project

`mazwi` is identified in README.md as an Andela project.
Apart from this guidance, the repository contains only that README; TODO: document its purpose, features, and technology stack when established.

## 2. Install, run, build and test

No source code, dependency manifest, scripts, CI configuration, or test suite exists yet.

- Install: TODO - no command established.
- Run: TODO - no command established.
- Build: TODO - no command established.
- Test: TODO - no command established.
- Lint: TODO - no command established.

Do not assume a language, package manager, or framework. Verify commands when implementation is added.

## 3. Code style and conventions

- README.md uses Markdown with an ATX heading (`# mazwi`) and a short project description.
- TODO: establish code formatting, naming, imports, directory structure, and test conventions once source code exists.
- Keep documentation concise and distinguish observed facts from TODOs.

## 4. Git rules

The repository uses `main`; the owner authorized using it instead of `master`.
The commands below are required workflow templates, not claims of executed project validation.
On Windows PowerShell without `&&` support, run each chained command separately and stop on failure.

### Issue first

- Before writing any code, and before creating a branch, there must be a GitHub issue.
- Check for an existing one first: `gh issue list --search "<keywords>"`. Use it if it exists.
- If none exists, create one: `gh issue create --title "<short summary>" --body "<the problem, the proposed change, the acceptance criteria>"`.
- No issue number, no code. If `gh` is not available, or the repo is not on GitHub, stop and ask the user. Do not skip this step.

### Branching

- Always branch from an up-to-date main: `git switch main && git pull && git switch -c <branch>`.
- Never branch from whatever is checked out, and never from `uat`.
- Name the branch `<type>/<issue-number>-<short-slug>`, for example `fix/42-login-timeout` or `feat/57-dark-mode`.
- One branch holds one piece of work.

### Upstream tracking

- A working branch must never track main.
- On the first push, set the upstream: `git push -u origin <branch>`.
- Check it: `git rev-parse --abbrev-ref <branch>@{upstream}` must print `origin/<branch>`, never `origin/main`.
- If it tracks main, fix it: `git branch --unset-upstream <branch>`, then push again with `-u`.
- When creating a worktree, always pass `--no-track`, or the branch silently tracks origin/main: `git worktree add --no-track <path> -b <branch> origin/main`.

### Commits and pull requests

- Never commit or push directly to main, and never merge into main locally.
- Every commit message references the issue, for example `Refs #42`.
- Before opening a PR, run the tests and the linter on the files you changed.
- To release, push the branch and open a PR targeting main. The PR body must include `Closes #<issue-number>`.

### UAT (only if the repo has a `uat` branch)

- To test, merge the working branch into `uat`.
- Make fixes on the working branch, then merge it into `uat` again.
- Never merge `uat` into main.

## 5. Documentation

Whenever the work involves the OpenAI API, the Apps SDK or Codex, use the openaiDeveloperDocs MCP without being asked.
