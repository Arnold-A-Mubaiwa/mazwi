# Password reset and email verification

Issue #47 lets artists recover a forgotten password and confirm their email address.

## For artists

- **Confirm your email.** Signing up sends a confirmation link. Until it is opened, Studio and Account show "Please confirm your email address…" with **Send the link again**. The account works fully either way.
- **Forgot your password?** On the sign-in page, enter your email. If an account uses it, a link arrives to choose a new password. The answer on screen is the same whether or not the account exists.
- **Choosing a new password** signs you out on every device and also confirms your email, since you proved you read it.

Links work **once**. A reset link lasts one hour and a confirmation link three days. Asking again replaces any earlier unused link of the same kind.

## Email on this local demo

There is no email provider yet. Messages are written as `.eml` files that any mail program opens:

| Running as | Folder |
| --- | --- |
| The app (`Start-Mazwi.ps1`) | `.local/outbox` (or `Mazwi:OutboxPath`) |
| .NET tests | a temporary folder |
| Browser tests | `.local/browser-outbox` |

Links point at `Mazwi:PublicBaseUrl` (default `http://127.0.0.1:3000`), **never** at the request's Host header, which a client could forge to send a reset link to another website. Set it to the real address when hosting.

Before real artists depend on it, replace `LocalOutboxEmailSender` with an adapter for a real provider behind the same Domain `IEmailSender` interface.

## Security

- Secrets are 256-bit random values. Only their SHA-256 hashes are stored (`account_tokens`, migration 011), so a copy of the database cannot reset passwords.
- Using a link and saving the new password happen in one transaction, so two requests with the same link cannot both succeed.
- Each artist has a **security stamp** stored in the session cookie. Changing the password changes the stamp, and every session with the old one is signed out.
- Reset, confirm and resend requests share the sign-in rate limit.
- Email subjects cannot contain line breaks, so nothing can be injected as extra email headers.

| Endpoint | Purpose |
| --- | --- |
| `POST /api/account/password-reset` | `{ email }`; always 202 |
| `POST /api/account/password-reset/confirm` | `{ token, password }`; 204, or 400 for an expired or used link |
| `POST /api/account/verify-email` | `{ token }`; 204 or 400 |
| `POST /api/account/verification-email` | Signed in; sends a new confirmation link (202) |
| `GET /api/account/me` | Now includes `emailVerified` |

## Tests

- **Domain:** token issuing, lifetimes, the expiry boundary, single use, hashing, the password change rotating the stamp, verification, and the email subject rule.
- **Application:** the reset email uses the configured site; unknown emails send nothing; reset, expired, wrong and reused links; weak passwords; verification; sign-up sends a link, and a failed email doesn't block sign-up.
- **Repository (real PostgreSQL):** replacing links, the transactional single use, cascade on account deletion, and the `.eml` format.
- **Endpoints:** status codes, plus a real round-trip: sign up, verify, reset, reused link refused, a second device signed out, old password refused, new password works.
- **Browser:** `e2e/recovery.spec.ts` covers the confirmation link from the outbox, the banner going away, the forgot-password flow, mismatched passwords, the reset, a reused link, and the same answer for an unknown email.
