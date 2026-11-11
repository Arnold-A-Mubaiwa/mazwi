# Level 3

Level 3 makes Mazwi ready for real artists. Artists control what they share and can always get back into their account; viewers can flag problems for a moderator; and the app can run on a server behind HTTPS.

| Feature | Issue / PR | Guide |
| --- | --- | --- |
| Separate app and test databases | #45 / #51 | [databases.md](databases.md) |
| Unpublish and delete stories and the account | #46 / #52 | [artist-control.md](artist-control.md) |
| Password reset and email confirmation (local outbox) | #47 / #53 | [password-reset.md](password-reset.md) |
| Hosting: trusted proxy, secure cookie, HSTS, guide | #48 / #54 | [hosting.md](hosting.md) |
| Report a story and moderation | #49 / #55 | [moderation.md](moderation.md) |

## The journey

1. **Sign up** and open the confirmation link from your email (locally, the `.eml` file in `.local/outbox`).
2. **Upload**, write the words and **approve**. The story is in the feed.
3. A viewer presses **Report this story**.
4. A **moderator** (a confirmed email listed in `Mazwi:Moderators`) sees it at `/moderation` and can:
   - **hide** it, which removes it from the feed while the artist sees "Hidden for review";
   - **restore** it, which brings it back;
   - **close** the reports.
5. Forgot your password? **Reset** it from the link in your email; every other session signs out.
6. **Unpublish** a story, or **delete** it with all its recordings after typing its title.
7. **Delete your account** and every story with your password.

## For the owner after upgrading

- **Your stories:** since #45 the app has its own `mazwi` database. Your four stories were copied there unowned. Sign up at `/signup`, then run `./scripts/Claim-Stories.ps1 -Email you@example.com`.
- **Being a moderator:** add your email to `Mazwi__Moderators` in the ignored `.env`, restart, and confirm your email through `.local/outbox`.
- **Browser tests** need `MAZWI_E2E_ARTIST_PASSWORD` and `MAZWI_E2E_OTHER_ARTIST_PASSWORD` in `.env` as before. They run the API in the `Testing` environment against `mazwi_tests`, with emails in `.local/browser-outbox`.

## How it is tested

`web/e2e/level-3.spec.ts` runs the whole lifecycle with a brand-new artist and **nothing faked**: real API, database, recording files and outbox emails. It covers the confirmation link, upload, approval, an anonymous report, a moderator hiding and restoring, the artist seeing "Hidden for review", a password reset, unpublishing, deleting the story (its file is removed from disk), and deleting the account.

Each feature also has its own .NET tests on all four layers (with real PostgreSQL) and browser specs. At the close of Level 3 there are 601 .NET tests, 99 web unit tests and 45 browser tests.

## Still needed before real artists rely on it

- **A real email provider.** Email goes to a local `.eml` outbox; a provider adapter behind `IEmailSender` is the main remaining blocker. Without it, artists won't receive reset or confirmation emails on a server.
- The **hosting checklist** in hosting.md: backups of the database and recordings, closed ports, a production environment, data protection keys, moderators set.
- The Level 1 **live check** with a real OpenAI key.
- Still undefined, and not started: Story Twins, voice stamps or cloning, the Android bubble.

## Release

After this merges, `level-3` is tagged (annotated) on the merge commit.
