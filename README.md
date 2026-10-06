# Yard Ops

[![CI](https://github.com/poker-kid-100717/yard-ops/actions/workflows/ci.yml/badge.svg)](https://github.com/poker-kid-100717/yard-ops/actions/workflows/ci.yml)

A clean-room portfolio yard execution application: yard board, gate in/out, dock inspections, and a live integration with an LTL planning service.

It is the standalone version of Yard Ops from [logistics-portfolio-suite](https://github.com/poker-kid-100717/logistics-portfolio-suite), and integrates with [ltl-planner](https://github.com/poker-kid-100717/ltl-planner) over a signed, versioned HTTP contract. It also runs on its own: without an LTL Planner the yard workflows still work and LTL features report that the planner is unavailable.

## What it does

A working yard management tool. Everything below reads and writes a real database.

| Menu | Pages |
| --- | --- |
| **Overview** | Yard Board: every parking spot and dock door with the trailer in it, plus yard numbers |
| **Operations** | Gate (check in/out, pre-advise expected trailers) · Trailers (list and a detail page to move, load, inspect, hold or release, with history and LTL candidates) · Inspections (checklist results) |
| **Integration** | LTL Outbox (every event for LTL Planner and its delivery state) · TMS Trailers (read-only) |

Settings sits at the bottom of the menu.

Rules the API enforces: trailers move Expected → Arrived → At door → Loading → Ready → Departed, with holds from Arrived, At door or Loading; anything else is a 409 that says what the trailer can do next. A spot holds one trailer (a unique index enforces it). Every applicable checklist item must pass for a trailer to be Ready, and a failed inspection puts it on hold.

## Demonstrates

- a transactional outbox: each event for LTL Planner is written in the same database transaction as the yard change that caused it, then delivered with an HMAC-SHA256 signature and bounded exponential backoff; yard work never waits on LTL
- the Yard -> LTL v1 contract, unchanged: the same signed payload and header LTL Planner already verifies, and synchronous candidate lookup
- .NET 10 minimal API with EF Core 10 on PostgreSQL (migrations applied by the deploy pipeline as the owner; the running app connects as a least-privilege role), ProblemDetails validation, and 409s for rule violations
- Angular 22 routed app: lazy-loaded pages, signals, one accessible drawer for every form, light and dark themes, tablet and phone layouts
- public-demo safeguards: per-client write rate limits, body size limits, a daily reset and an hourly outbox pass from Cloudflare cron triggers
- optional read-only Alvys Trailers Search through an OAuth 2.0 client-credentials adapter
- integration tests against both SQLite and PostgreSQL in CI, with a fake LTL endpoint that checks the exact signed payload

See [docs/architecture.md](docs/architecture.md).

## Run locally

```bash
cp .env.example .env
docker compose up --build
```

Compose starts PostgreSQL too; the API applies migrations and seeds a fictional yard on first start.

- UI: http://localhost:4203
- API: http://localhost:5103 (health at `/health`)

To exercise the integration, start the [ltl-planner](https://github.com/poker-kid-100717/ltl-planner) compose stack as well (it listens on host port 5102, which is Yard's default `LTL_BASE_URL`) and use the same `YARD_LTL_SIGNING_KEY` in both `.env` files.

Without Docker:

```bash
ASPNETCORE_URLS=http://localhost:5103 dotnet run --project api   # expects LTL at http://localhost:5102/
cd web && npm install && npm start   # UI on http://localhost:4203; /api proxies to :5103
```

Demo mode is the default and needs no credentials. To enable live, read-only Alvys reads set `ALVYS_MODE=Live`, `ALVYS_CLIENT_ID`, and `ALVYS_CLIENT_SECRET`. Credentials stay server-side; the Angular app never sees them.

## Tests

```bash
dotnet test tests/Portfolio.Yard.Api.Tests.csproj                                    # SQLite
TEST_DATABASE_URL=postgres://user:pass@localhost:5432/yard_test dotnet test tests/Portfolio.Yard.Api.Tests.csproj  # PostgreSQL
```

## Deploy to Cloudflare

Deployment runs from GitHub Actions on every push to `main` (`.github/workflows/deploy-cloudflare.yml`). It builds the Angular app, deploys a Worker that serves it from the edge, runs the .NET API in a Cloudflare Container, and smoke-tests the result, including a real Yard -> LTL call when an LTL URL is configured.

Repository **secrets**:

| Secret | Required | Purpose |
| --- | --- | --- |
| `CLOUDFLARE_API_TOKEN` | yes | Wrangler deploys |
| `CLOUDFLARE_ACCOUNT_ID` | yes | Wrangler deploys |
| `DATABASE_URL` | recommended | PostgreSQL URL the running app uses: a Neon **pooled** URL ending in `?sslmode=require`, for a role with data rights only (see below). Without it the app runs on a demo database that resets whenever the container restarts. |
| `DATABASE_URL_UNPOOLED` | recommended | The owner's **direct** (non-pooled) URL. The deploy applies migrations with it before the new container starts. Without it the app migrates itself on startup (and then needs DDL rights). |
| `DEMO_RESET_TOKEN` | no | Token for `POST /api/admin/reset-demo`. The demo yard reset nightly at 08:29 UTC; a per-deploy token is generated when unset. |
| `YARD_LTL_SIGNING_KEY` | recommended | Signs events sent to LTL. Must equal the key in the ltl-planner repo. Signatures cover an `X-Portfolio-Timestamp` header and the body; LTL rejects anything more than five minutes old. Generated per deploy when absent. |
| `ALVYS_CLIENT_ID` / `ALVYS_CLIENT_SECRET` | no | Live, read-only Alvys mode |

Database roles (Neon or any PostgreSQL): the owner role in `DATABASE_URL_UNPOOLED` owns the schema; the app role in `DATABASE_URL` only reads and writes rows:

```sql
CREATE ROLE yard_app LOGIN PASSWORD '...';
GRANT CONNECT ON DATABASE yard TO yard_app;
GRANT USAGE ON SCHEMA public TO yard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO yard_app;
ALTER DEFAULT PRIVILEGES FOR ROLE yard_owner IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO yard_app;
ALTER DEFAULT PRIVILEGES FOR ROLE yard_owner IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO yard_app;
```

Cloudflare cron triggers handle the nightly reset, an hourly outbox pass and a keep-warm ping to `/health/ready` every five minutes during weekday business hours (14:00-23:55 UTC), so the first visitor does not wait for a cold container and a suspended database.

Repository **variables** (optional):

| Variable | Purpose |
| --- | --- |
| `APP_HOST` | Custom domain such as `yard.example.com`. When unset the app is served from its `workers.dev` URL. |
| `LTL_BASE_URL` | The deployed LTL Planner, for example `https://ltl.example.com/`. When unset Yard runs standalone. |

Deploy LTL Planner first so Yard's smoke test can reach it. Until the Cloudflare secrets exist the deploy job skips cleanly. `scripts/cloudflare-deploy.sh` can also be run locally with the same environment variables.

## Repository structure

```text
api/          .NET 10 API: Data/ (EF Core model, migrations, demo seed), Endpoints/, trailer rules, outbox, LTL client, Alvys adapter
tests/        xUnit unit and integration tests
web/          Angular 22 UI
cloudflare/   Worker + Container definition for Cloudflare hosting
scripts/      deploy and publication-safety scripts
docs/         architecture, Alvys public API notes, clean-room boundary
```

## Clean-room boundary

Written from generic workflow requirements and public vendor documentation. It contains no former-employer source code, data, credentials, internal URLs, customer names, or company-specific business rules. See [docs/clean-room-boundary.md](docs/clean-room-boundary.md) and [NOTICE.md](NOTICE.md).

## License

MIT
