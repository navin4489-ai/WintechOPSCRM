# Deploying Wintech POS to Render

Target service: **`the-bathany`** → `https://the-bathany.onrender.com`

The repo root (`ORO_POS_Solution/`) contains everything Render needs:

| File | Purpose |
| --- | --- |
| `render.yaml` | Blueprint: web service + fresh PostgreSQL database + env vars |
| `Dockerfile` | One image serving the .NET API **and** the Angular SPA |
| `web/oro-pos-web/dist/oro-pos-web/browser/` | Prebuilt Angular bundle (copied into `wwwroot`) |
| `scripts/build-web.ps1` | Rebuild that bundle after changing web source |

## 1. Connect the repo (one time)

Render Dashboard → **New +** → **Blueprint** → connect
`https://github.com/navin4489-ai/WintechOPSCRM` → accept the suggested `render.yaml`.

Render then provisions:

- web service `the-bathany` (Docker runtime, `main` branch, health check `/health`)
- Postgres instance `the-bathany-db` (database `oro_pos`)
- `DATABASE_URL` wired from the database into the service
- `Jwt__Key` generated and stored by Render

Because the blueprint names the **existing** service `the-bathany`, Render applies the
config to your current service instead of creating a duplicate.

> If the sync errors with *"runtime cannot be changed"*, the current service is a
> **static site** (runtime is immutable there). In that case use **New + → Web Service**
> → connect the same repo → Runtime: **Docker** → Dockerfile path `./Dockerfile`,
> then create the Postgres instance separately and copy its connection string into a
> `DATABASE_URL` env var.

## 2. Verify the first deploy

1. Deploy log should show `[Startup] RESET_DB_ON_START=true — schema dropped...`
   followed by `[Startup] Database migration applied.`
2. Open `https://the-bathany.onrender.com/health` → `{"status":"healthy",...}`
3. Open the site root → Angular login screen
4. Sign in with `admin` / `Admin@123`

> **Free plan cold start:** the first request after idle takes ~30-50 s and may
> return `503` while the instance wakes up. Retry after a few seconds.

### Free-plan limits that affect this blueprint

- **One Free Postgres per workspace.** If `the-bathany-db` already exists as a
  Free instance (or another one does), the sync fails with a plan conflict.
  Either delete the old Free database first, or change `plan: free` under
  `databases:` in `render.yaml` to a paid plan (e.g. `0.1c-256mb`).
- **Free Postgres expires 30 days after creation**, then a 14-day grace period
  before Render deletes it (data included). Upgrade before then if this data
  matters.
- **Free web services suspend** once the 750 monthly free instance hours run
  out, which is one plausible cause of the current `503` on the live URL.
- Free Postgres has **no backups** and a **1 GB** storage cap.

## 3. Turn the data wipe OFF

`RESET_DB_ON_START` is set to `true` **only so this first deploy starts from an
empty database**. As long as it stays `true`, *every* deploy wipes all data.

After verifying step 2, set it to `false` in `render.yaml` (or the service's
Environment tab) and redeploy.

## 4. Deploying later changes

- **Backend / API** → commit + push to `main`; Render auto-deploys.
- **Web UI** → rebuild the bundle first, then commit:

  ```powershell
  pwsh ./scripts/build-web.ps1
  git add -A; git commit -m "ui: ..."; git push
  ```

  Pushing web source changes **without** rebuilding leaves the old bundle live,
  because the Docker image ships the committed `dist/`.

## Local development

```powershell
# API (needs a local Postgres, see ConnectionStrings:DefaultConnection)
cd backend; dotnet run --project ORO_POS.API --urls http://localhost:5000

# Web (proxies /api -> localhost:5000, see proxy.conf.json)
cd web/oro-pos-web; node node_modules/@angular/cli/bin/ng.js serve
```
