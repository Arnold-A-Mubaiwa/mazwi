# Artist accounts

Issue #33 adds artist accounts. Only a signed-in artist can upload, and only a story's owner can open its editor, run AI drafts, upload recordings or approve it. The feed, story pages and **Our story** stay public; viewers never need an account.

## Using it

1. Choose **Sign in** in the header, then **Create an account**. Passwords are 10–128 characters.
2. Upload, Studio and the story editor send signed-out visitors to sign in and bring them back afterwards.
3. Studio lists only your own stories. Opening another artist's editor shows "This story belongs to another artist."
4. **Sign out** in the header ends the session.

## Stories saved before accounts

Migration 008 gives every existing story an empty owner. Nobody can edit those stories until they are claimed, but approved ones stay in the feed. After creating your account, claim them once from the repository root in PowerShell (the script loads the ignored `.env` like `Start-Mazwi.ps1`):

```powershell
./scripts/Claim-Stories.ps1 -Email you@example.com
```

It prints how many stories it claimed and only claims stories that have no owner. An unknown or invalid email prints a one-line error and exits with code 1. Under the hood it runs the API with `--claim-stories <email>`. A command is used instead of "first account claims everything" because the test database is shared with the local app, and a test account must never claim real stories.

## How it works

- Passwords are hashed with ASP.NET Core's `PasswordHasher` (salted PBKDF2) behind the Domain `IPasswordHasher`. Identity's Entity Framework stores are not used. Emails are stored lower-case.
- Sign-in sets the `mazwi_session` cookie: HttpOnly, SameSite=Lax, Secure only when the API itself receives HTTPS (see Limits), 14-day sliding expiry. A session for a deleted account is rejected.
- Cross-site requests do not carry the Lax cookie, which is the CSRF defence for this JSON and multipart API.
- A wrong email and a wrong password get the same message, and an unknown email still pays for one password hash so response time does not reveal which accounts exist. Sign-up and sign-in are limited to 10 requests per minute per client address (`Mazwi:AccountRequestsPerMinute`); the 11th returns 429.
- `[StoryOwner]` checks ownership in the Domain (`Story.IsOwnedBy`) before an action runs. Signed out returns 401, another artist's story 403, a missing story 404.

| Endpoint | Purpose |
| --- | --- |
| `POST /api/account/signup` | `{ email, password }`; 409 if the email is taken |
| `POST /api/account/signin` | `{ email, password }`; 401 if they do not match |
| `POST /api/account/signout` | Ends the session |
| `GET /api/account/me` | The signed-in artist, or 401 |

## Tests

- .NET: Domain email, password and ownership rules; Application sign-up, sign-in, claims and owner checks; real PostgreSQL artists, ownership, listing and claims; endpoint 401/403/404 for every story route, cookies, rate limits and a real database round-trip with two artists.
- Browser: `e2e/auth.setup.ts` signs in a stable fixture artist (`e2e-artist@mazwi.test`) before the other specs, so repeated runs do not add accounts. `e2e/access.spec.ts` covers the sign-in redirect, wrong passwords, a taken email, the public feed and another artist being refused.
- Browser fixture stories created from now on belong to the fixture artist and stay out of your Studio. Since #45 the app has its own database, so test fixtures are never in your Studio (docs/databases.md).
- The fixture passwords come from `MAZWI_E2E_ARTIST_PASSWORD` and `MAZWI_E2E_OTHER_ARTIST_PASSWORD` in the ignored `.env` (see `.env.example`). The browser run fails if they are missing.

## Limits

- No password reset or email verification yet. Keep the app on localhost or a trusted network until those exist.
- No account deletion screen. Deleting an artist row leaves their stories unowned until claimed.
- The local app and tests used to share the `_tests` database; issue #45 separated them (docs/databases.md).
- **Behind the Next.js proxy every request reaches the API from 127.0.0.1**, so the sign-in rate limit is one shared bucket for the whole site. Next passes a client-sent `X-Forwarded-For` through unchanged, so it cannot be trusted. Before hosting, put a trusted reverse proxy in front that sets forwarded headers, and enable `UseForwardedHeaders` for it.
- For the same reason the API never sees HTTPS, so the cookie is not marked `Secure` yet. That also needs the trusted proxy before hosting.
