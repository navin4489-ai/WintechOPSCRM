# Live deployment: pos.wintechinov.com

## Current setup

| Piece | Where | Notes |
|---|---|---|
| Web + API | Render web service `wintech-pos` (free, Docker) | Built from the public repo `navin4489-ai/WintechOPSCRM`, branch `main`. Auto-deploys on push. |
| Database | Render Postgres `wintech-pos-db` (free) | Connected via `DATABASE_URL` (keyword or `postgres://` form). |
| Domain | Cloudflare zone `wintechinov.com` | `pos` CNAME → `wintech-pos-zpxf.onrender.com`, DNS only (grey cloud). Render issues TLS. |
| Mobile | `mobile/releases/Wintech-POS-1.6.0.apk` | Release-signed; API default `https://pos.wintechinov.com/api`. |

The old origin server `103.21.58.194` (Plesk) is no longer used. Its FTP and panel
credentials were rejected, so deployment moved to Render. To move back, re-point the
Cloudflare `pos` record to the server and deploy the publish bundle there.

## Configuration (Render service environment)

| Key | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Jwt__Key` | 48+ random bytes, base64. Rotating it signs everyone out. |
| `RESET_DB_ON_START` | `false` (never enable on live data) |
| `INITIAL_ADMIN_PASSWORD` | Used only when the database is new. Change the admin password after first login. |
| `DATABASE_URL` | Postgres connection (Render "Internal Database URL") |

## Behaviour to know about

- **Cold starts.** Free web services sleep after about 15 minutes idle. The first request
  after that can take 30 to 60 seconds.
- **Free Postgres expires after 30 days.** Render states free databases are for testing.
  Before the expiry date, either upgrade the database or export and restore it into a
  paid instance. The current expiry date is shown on the database page.
- **Schema.** A Postgres database is created from the EF model on first boot
  (`EnsureCreated`). SQLite (self-hosted) uses EF migrations. Schema changes on Postgres
  therefore need a deliberate reset or a new migration path before go-live.
- **Outlets.** Outlets are created through Super Admin onboarding
  (`POST /api/super-admin/outlets`), which also creates the outlet admin login.

## Redeploy / verify

```powershell
curl.exe -s https://pos.wintechinov.com/health
```

Expected: `{"status":"healthy", ...}`. Then sign in as the Super Admin and check that the
outlet list and menu load. For the menu seed, run `scripts/seed-krishana-menu.py` after
pointing its `BASE` at `https://pos.wintechinov.com/api`. It is idempotent.

## Credentials

Generated secrets live outside the repo. Nothing secret is committed.
The Render dashboard holds the live values, and the local copies are in `%TEMP%`.
