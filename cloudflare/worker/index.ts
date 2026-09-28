import { Container } from "@cloudflare/containers";

export interface Env {
  ASSETS: Fetcher;
  API: DurableObjectNamespace<YardApi>;
  LTL_BASE_URL?: string;
  YARD_LTL_SIGNING_KEY?: string;
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
} satisfies ExportedHandler<Env>;
