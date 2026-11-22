# Testing plan: Wintech POS

Scope: the API, the Angular web app (POS, public QR menu, admin), the Flutter mobile app,
and the live deployment at https://pos.wintechinov.com.

## 1. Automated

| Layer | Tool | Command | Count | Status |
|---|---|---|---|---|
| API / domain / tenant isolation | xUnit (EF InMemory) | `dotnet test backend/ORO_POS.Tests` | 97 | Passing |
| Web end to end | Playwright | `npx playwright test` in `web/oro-pos-web` | 29 | Run before each web change |
| Mobile | `flutter test` | `flutter test` in `mobile` | widget + kitchen bell + documents | Run before each APK build |

## 2. Live smoke (run after every deploy)

Run against `https://pos.wintechinov.com`:

1. `GET /` returns 200 and the SPA title.
2. `GET /health` returns `healthy`.
3. Owner login (`POST /api/auth/login`) returns a token.
4. Outlet-admin login returns a token and sees only its own outlet (tenant isolation).
5. Menu for outlet 2 returns the seeded items (93 at the last seed).
6. Public QR menu page loads and shows INR prices.
7. Create one order, send to kitchen, and confirm the kitchen bell event (web and mobile).
8. Generate a receipt or PDF and confirm it is outlet-only and shows ₹.

## 3. Manual UAT (per release)

| Area | Checks |
|---|---|
| Auth | Username or email login; wrong password is rejected with a generic message; password change works; rate limit returns 429 after bursts. |
| POS | Table open, add items, edit cart, one-tap cart sheet on long menus, split and merge tables, void and refund need permission. |
| Kitchen | Order appears on KDS; bell and siren play; status moves through prepared and served. |
| Money | All amounts show ₹ (INR); tax and discount math matches the receipt. |
| Admin | Super Admin creates an outlet with its admin; the outlet admin cannot see other outlets. |
| Mobile | Install the APK from `releases/`; sign in against `pos.wintechinov.com`; first request may take about 60 s (free tier cold start). |

## 4. Non-functional

- **Cold start:** first response under 90 s; subsequent responses under 1 s.
- **Rate limits:** login 15 per minute per IP; public order 10 per minute; public read 120 per minute.
- **Security headers and TLS:** HTTPS only (Render redirects HTTP); `/data` and `*.db` not served.
- **Backups:** Render free Postgres has no automated backups. Export with `pg_dump` before the 30-day expiry and periodically after.

## 5. Exit criteria for a release

- All automated suites green.
- Live smoke steps 1 to 8 pass.
- No open Sev-1 or Sev-2 issues.
- Admin password rotated from the seeded value; `Jwt__Key` is not the sample key.
