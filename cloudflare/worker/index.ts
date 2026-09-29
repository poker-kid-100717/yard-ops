import { Container } from "@cloudflare/containers";

export interface Env {
  ASSETS: Fetcher;
  API: DurableObjectNamespace<YardApi>;
  LTL_BASE_URL?: string;
  YARD_LTL_SIGNING_KEY?: string;
  DATABASE_URL?: string;
  DEMO_RESET_TOKEN?: string;
  ALVYS_CLIENT_ID?: string;
  ALVYS_CLIENT_SECRET?: string;
}

export class YardApi extends Container<Env> {
  defaultPort = 8080;
  sleepAfter = "10m";
  pingEndpoint = "localhost/health";

  constructor(ctx: DurableObjectState<{}>, env: Env) {
    super(ctx, env);
    this.envVars = {
      ASPNETCORE_ENVIRONMENT: "Production",
      // Without an LTL deployment the API keeps its default URL; LTL calls then fail
      // fast, candidate lookups return 503, and outbox events wait with backoff.
      ...(env.LTL_BASE_URL ? { Ltl__BaseUrl: env.LTL_BASE_URL } : {}),
      Ltl__SigningKey: env.YARD_LTL_SIGNING_KEY ?? "portfolio-bootstrap-only",
      // Without DATABASE_URL the API runs on a throwaway demo database.
      ...(env.DATABASE_URL ? { ConnectionStrings__Default: env.DATABASE_URL } : {}),
      ...(env.DEMO_RESET_TOKEN ? { Demo__ResetToken: env.DEMO_RESET_TOKEN } : {}),
      Alvys__Mode: env.ALVYS_CLIENT_ID && env.ALVYS_CLIENT_SECRET ? "Live" : "Demo",
      ...(env.ALVYS_CLIENT_ID ? { Alvys__ClientId: env.ALVYS_CLIENT_ID } : {}),
      ...(env.ALVYS_CLIENT_SECRET ? { Alvys__ClientSecret: env.ALVYS_CLIENT_SECRET } : {}),
    };
  }
}

const api = (env: Env) => env.API.getByName("api");

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    if (url.pathname.startsWith("/api/") || url.pathname === "/health" || url.pathname.startsWith("/health/")) {
      return api(env).fetch(request);
    }
    return env.ASSETS.fetch(request);
  },

  // Crons (wrangler.template.jsonc): the daily demo reset, and an hourly outbox pass that wakes the
  // container so events held while LTL Planner was unreachable are still delivered.
  async scheduled(controller: ScheduledController, env: Env, ctx: ExecutionContext): Promise<void> {
    const reset = controller.cron === RESET_CRON;
    if (reset && !env.DEMO_RESET_TOKEN) return;
    const request = reset
      ? new Request("http://api/api/admin/reset-demo", { method: "POST", headers: { "X-Demo-Reset-Token": env.DEMO_RESET_TOKEN! } })
      : new Request("http://api/api/outbox/drain", { method: "POST" });
    ctx.waitUntil(
      api(env).fetch(request).then(response => {
        if (!response.ok) console.error(`${reset ? "Demo reset" : "Outbox drain"} failed: ${response.status}`);
      }),
    );
  },
} satisfies ExportedHandler<Env>;

const RESET_CRON = "29 8 * * *";
