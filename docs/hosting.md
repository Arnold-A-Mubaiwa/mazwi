# Hosting Mazwi

Issue #48 prepares Mazwi to run on a server behind HTTPS. Nothing here changes local use: it only switches on when `Mazwi:Hosted` is `true`.

## The shape

```
visitor ──https──▶ Caddy (public, :443) ──▶ Next.js 127.0.0.1:3000 ──/api──▶ Mazwi API 127.0.0.1:5046 ──▶ PostgreSQL
```

- **Caddy** (`deploy/Caddyfile`):
  - Gets and renews the certificate and redirects http to https.
  - Adds HSTS and security headers.
  - Replaces any `X-Forwarded-For` a visitor sends with their real address, and sets `X-Forwarded-Proto: https`.
- **Next.js** must listen on `127.0.0.1` only; `npm run start` already does. It passes `X-Forwarded-For` and `-Proto` to the API unchanged (checked in #48 with an echo server) and connects to the API from `127.0.0.1`.
- **The API** must also listen on `127.0.0.1` only, for example `--urls http://127.0.0.1:5046`. Port 5046 and port 3000 must not be reachable from outside, or a visitor could send forged forwarded headers straight to them.

## API settings when hosted

See `deploy/mazwi.env.example`. The important ones:

| Setting | Why |
| --- | --- |
| `Mazwi__Hosted=true` | The session cookie is always `Secure`, and HSTS is sent on HTTPS responses |
| `Mazwi__TrustedProxies=127.0.0.1` | Only Next.js on this machine may pass on the visitor's address and `https`. Every other sender's forwarded headers are ignored. |
| `Mazwi__PublicBaseUrl=https://…` | Emailed reset and confirmation links point here, never at the request's Host header |
| `ConnectionStrings__Mazwi`, `MediaStorage__Root` | Production database (not `*_tests`; the API refuses those) and recording folder |

The API **refuses to start** when hosted without trusted proxies, with a non-HTTPS `PublicBaseUrl`, or with a `TrustedProxies` entry that is not an IP address.

With the forwarded address trusted, the sign-in and reset limit (10 a minute) applies per visitor instead of being shared by the whole site (Level 2 limit, now fixed).

## Running

1. Install the .NET 10 runtime, Node 24, PostgreSQL and Caddy.
2. Build: `dotnet publish src/Mazwi.WebApi -c Release -o /srv/mazwi/api` and `npm --prefix web ci && npm --prefix web run build`.
3. Migrate once per release: `dotnet /srv/mazwi/api/Mazwi.WebApi.dll --migrate` (with the settings above).
4. Run as services (systemd, or Windows services):
   - API: `dotnet Mazwi.WebApi.dll --urls http://127.0.0.1:5046`
   - Web: `npm --prefix web run start` with `MAZWI_API_URL=http://127.0.0.1:5046` set **at build time**, because Next fixes the rewrite target then.
5. Start Caddy with `deploy/Caddyfile`, with your domain in place of `mazwi.example`.

## Checklist before real artists use it

- [ ] **Email:** replace `LocalOutboxEmailSender` with a real provider behind `IEmailSender`. Until then, reset and confirmation emails are files on the server (`Mazwi__OutboxPath`) and artists will not receive them.
- [ ] Separate production database with backups, and backups of `MediaStorage__Root` (recordings exist only there).
- [ ] Ports 3000 and 5046 are closed to the outside, and only Caddy listens publicly.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`; secrets in the service environment, not in files in the repository.
- [ ] Set `Mazwi__Moderators` to the people responsible for reports (docs/moderation.md); they must confirm their email first.
- [ ] Run the Level 1 live check with a real OpenAI key and confirm the costs.
- [ ] Data protection keys (they encrypt session cookies) are kept across restarts, so sign-ins survive deploys. On a single server the default user-profile key folder works; with several servers, share them.

## Tests

`HostingTests`:
- configuration rules (local defaults, parsing, each startup refusal);
- forwarded headers only from the listed proxy, with the loopback defaults removed;
- through a simulated proxy: per-visitor rate limits, a forged address from an untrusted sender ignored, and a `Secure` cookie plus HSTS when hosted but neither locally.
