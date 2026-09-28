# Yard Ops

[![CI](https://github.com/poker-kid-100717/yard-ops/actions/workflows/ci.yml/badge.svg)](https://github.com/poker-kid-100717/yard-ops/actions/workflows/ci.yml)

A clean-room portfolio yard execution application: yard board, gate in/out, dock inspections, and a live integration with an LTL planning service.

It is the standalone version of Yard Ops from [logistics-portfolio-suite](https://github.com/poker-kid-100717/logistics-portfolio-suite), and integrates with [ltl-planner](https://github.com/poker-kid-100717/ltl-planner) over a signed, versioned HTTP contract. It also runs on its own: without an LTL Planner the yard workflows still work and LTL features report that the planner is unavailable.

## Demonstrates

- yard-board state and gate/dock workflows
- Angular 22 tablet-friendly operations UI
- .NET 10 minimal API
- optional read-only Alvys Trailers Search through an OAuth 2.0 client-credentials adapter
- synchronous Yard -> LTL candidate lookup
- asynchronous Yard -> LTL integration events with HMAC-SHA256 payload signatures
- an outbox with bounded exponential backoff, so local yard actions never depend on LTL being up
- Cloudflare Workers + Containers hosting deployed from GitHub Actions

See [docs/architecture.md](docs/architecture.md).

## Run locally

```bash
cp .env.example .env
docker compose up --build
```

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
dotnet test tests/Portfolio.Yard.Api.Tests.csproj
```

## Deploy to Cloudflare

Deployment runs from GitHub Actions on every push to `main` (`.github/workflows/deploy-cloudflare.yml`). It builds the Angular app, deploys a Worker that serves it from the edge, runs the .NET API in a Cloudflare Container, and smoke-tests the result, including a real Yard -> LTL call when an LTL URL is configured.

Repository **secrets**:

| Secret | Required | Purpose |
| --- | --- | --- |
| `CLOUDFLARE_API_TOKEN` | yes | Wrangler deploys |
| `CLOUDFLARE_ACCOUNT_ID` | yes | Wrangler deploys |
| `YARD_LTL_SIGNING_KEY` | recommended | Signs events sent to LTL. Must equal the key in the ltl-planner repo. Generated per deploy when absent. |
| `ALVYS_CLIENT_ID` / `ALVYS_CLIENT_SECRET` | no | Live, read-only Alvys mode |

Repository **variables** (optional):

| Variable | Purpose |
| --- | --- |
| `APP_HOST` | Custom domain such as `yard.example.com`. When unset the app is served from its `workers.dev` URL. |
| `LTL_BASE_URL` | The deployed LTL Planner, for example `https://ltl.example.com/`. When unset Yard runs standalone. |

Deploy LTL Planner first so Yard's smoke test can reach it. Until the Cloudflare secrets exist the deploy job skips cleanly. `scripts/cloudflare-deploy.sh` can also be run locally with the same environment variables.

## Repository structure

```text
api/          .NET 10 API (yard state, outbox, LTL client, Alvys adapter)
tests/        xUnit tests for the yard store, outbox, and signatures
web/          Angular 22 UI
cloudflare/   Worker + Container definition for Cloudflare hosting
scripts/      deploy and publication-safety scripts
docs/         architecture, Alvys public API notes, clean-room boundary
```

## Clean-room boundary

Written from generic workflow requirements and public vendor documentation. It contains no former-employer source code, data, credentials, internal URLs, customer names, or company-specific business rules. See [docs/clean-room-boundary.md](docs/clean-room-boundary.md) and [NOTICE.md](NOTICE.md).

## License

MIT
