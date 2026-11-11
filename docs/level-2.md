# Level 2

Level 2 makes Mazwi the artist's own space and gives viewers a fuller way to watch:
- artists sign in, and only they can change or approve their stories;
- they time subtitles line by line;
- video stories can be watched dubbed in the viewer's language;
- artists see how often their stories are played;
- Mazwi can be installed and stories shared.

Nothing goes public without the artist pressing **Approve**, as in Level 1.

| Feature | Issue / PR | Guide |
| --- | --- | --- |
| Artist accounts and protected Studio | #33 / #39 | [accounts.md](accounts.md) |
| Artist-timed subtitles | #34 / #40 | [subtitles.md](subtitles.md) |
| Dubbed video playback in the browser | #35 / #41 | [dubbing.md](dubbing.md) |
| Privacy-safe play statistics | #36 / #42 | [play-stats.md](play-stats.md) |
| Installable app and sharing | #37 / #43 | [pwa-sharing.md](pwa-sharing.md) |

## The journey

1. **Sign in or create an account.** Upload, Studio and the editor send signed-out visitors to sign in and bring them back.
2. **Upload** an audio or video recording. It is saved privately and belongs to you.
3. **AI first draft** of the transcript, which you correct (Level 1).
4. **Time the subtitles.** Press **Mark next line** as each line begins, or type the seconds, then save.
5. **Approve the original.**
6. **AI translation draft and AI voice** (Level 1), then **time the translation** against its voice.
7. **Approve the translation.** Tick "I speak …" only if true.
8. **Viewers watch.**
   - A video story with a translated voice plays dubbed: the original video with the voice, and the original sound off.
   - The current line shows under the player, with honest labels.
   - **Share this story** opens the share sheet or copies the link.
9. **Studio shows plays**, for example "1 play · 1 this week".

Any edit to text, timing or a recording removes approval until you approve again.

## Before using it after upgrading

Migrations 008–010 run automatically when you start Mazwi (`./scripts/Start-Mazwi.ps1`). Stories saved before accounts belong to nobody until you claim them:

1. Start Mazwi and create your account at `/signup`.
2. From the repository root in PowerShell, run once: `./scripts/Claim-Stories.ps1 -Email you@example.com`
3. Your stories appear in Studio again.

Since Level 3 (#45) the app has its own database; your stories were copied into it, so claiming no longer picks up test fixtures (see databases.md).

The browser tests need `MAZWI_E2E_ARTIST_PASSWORD` and `MAZWI_E2E_OTHER_ARTIST_PASSWORD` in the ignored `.env` (see `.env.example`).

## How it is tested

`web/e2e/level-2.spec.ts` runs the whole journey in a browser against the real API and database, starting signed out:
1. Sign in through the redirect.
2. Upload a WebM video, recorded in the browser because there is no ffmpeg.
3. AI transcript, corrected, timed and approved.
4. AI translation and AI voice; the voice is timed and approved without the speaker box.
5. Viewer side, in isiXhosa: the dubbed label, the "Not checked by an isiXhosa speaker" label, a play counted (204) and the subtitle line at 1.5 s.
6. Share through the share sheet.
7. "1 play · 1 this week" in Studio.

Only the three OpenAI calls are faked, as in Level 1. Each feature also has its own .NET tests on all four layers (with real PostgreSQL) and its own browser spec.

## Known limits

- **Not ready for public hosting yet.**
  - There is no password reset or email verification.
  - The app and tests shared one database (separated in Level 3, #45).
  - Behind the Next.js proxy the sign-in rate limit is site-wide and the cookie is not `Secure`. Fixing both needs a trusted reverse proxy (see accounts.md).
- **Dubbing** happens only in the browser: there is no downloadable dubbed file (no ffmpeg) and no lip-sync.
- **Subtitles** are timed by hand, one start per line.
- **Share target** cannot be checked on a real phone until Mazwi is served over HTTPS.
- **Not included** because they are still undefined: Story Twins, voice stamps or cloning, and the Android bubble.

## Release

After this merges, `level-2` is tagged on the merge commit. The Level 1 live check (one real story with an OpenAI key, see level-1.md) is still recommended.
