# App and test databases

Issue #45 ends the temporary arrangement where the local app and the tests shared one database.

| Setting (ignored `.env`) | Used by | Example database |
| --- | --- | --- |
| `ConnectionStrings__Mazwi` | The app (`Start-Mazwi.ps1`, the claim and import scripts) | `mazwi` |
| `MAZWI_TEST_CONNECTION_STRING` | .NET tests and the browser tests' API | `mazwi_tests` |

- The repository tests also create and use a second test database, `mazwi_import_tests`.
- The API refuses to start against a database whose name ends in `_tests`, unless it runs in the `Testing` environment (the .NET tests and the Playwright API server). This stops real stories landing where tests create and delete fixtures.
- `--migrate`, which `Start-Mazwi.ps1` runs, creates the app database if it does not exist and then applies the versioned migrations. The database user needs permission to create databases for that first run; otherwise create the database once yourself.

## Moving stories out of the old shared database

Stories you uploaded before #45 live in the old shared `_tests` database. `scripts/Import-Stories.ps1` copies the **unowned** stories by one artist name into the app database: the rows, translations, approvals, subtitle timings and play counts. The recordings already sit in `.local/media`, which the app has always used, so files are not copied.

```powershell
./scripts/Import-Stories.ps1 -ArtistName "Your name"          # lists what it would copy
./scripts/Import-Stories.ps1 -ArtistName "Your name" -Apply   # copies; running it again copies nothing new
```

Copied stories stay unowned. Create your account in the app (`/signup`), then claim them with `./scripts/Claim-Stories.ps1 -Email you@example.com`.

The original rows are left in the old database. The tests may delete them later; that does not affect the app.

## The owner's migration on 2026-11-11

- `ConnectionStrings__Mazwi` was switched from `mazwi_tests` to a new `mazwi` database, which the migrator created. The previous `.env` was kept at `.local/env.backup-before-45`.
- The import copied the four stories by "Arnold": Greeting, Greeting in Shona, Greeting Sho and Xhosa Greeting. Their four `.mp4` recordings were confirmed in `.local/media`.
- Synthetic test stories (for example by "Test storyteller", "Mazwi browser fixture", "Artist" or "Rudo") were not copied.

## Tests

- `DatabaseSafetyTests`: the guard refuses `*_tests` databases outside Testing, allows the app database, and checks the name suffix.
- `StoryImporterTests` (real PostgreSQL, two databases):
  - a dry run copies nothing;
  - apply copies the story with its translation notes, approval, subtitle timing and play counts, and the story stays unowned;
  - a second run reports "already here";
  - owned stories and other artists' stories are not copied.
