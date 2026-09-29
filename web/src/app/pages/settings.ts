import { Component, inject } from '@angular/core';
import { Session } from '../core/session';

@Component({
  selector: 'app-settings',
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Admin</p><h1>Settings</h1><p class="muted">How this demo is configured.</p></div>
    </header>
    @if (session.meta(); as meta) {
      <div class="settings-grid">
        <section class="card">
          <h2>Data storage</h2>
          <p><span class="badge" [attr.data-tone]="meta.storage.persistent ? 'good' : 'warn'">{{ meta.storage.mode }}</span></p>
          <p class="muted">{{ meta.storage.persistent ? 'Changes are saved to PostgreSQL.'
            : 'No database is configured, so the app uses a temporary demo database that resets when the server restarts.' }}</p>
          <p class="muted">Demo reset: {{ meta.demoReset.scheduled ? meta.demoReset.schedule : 'not scheduled' }}.</p>
        </section>
        <section class="card">
          <h2>LTL Planner</h2>
          <p class="muted">Events go to {{ meta.ltl.baseUrl }} over contract v1, signed with a shared HMAC key. If LTL is unreachable the yard keeps working
            and events wait in the outbox.</p>
        </section>
        <section class="card">
          <h2>Trailer lifecycle</h2>
          <p class="muted">Expected → Arrived → At door → Loading → Ready → Departed. Arrived, at-door and loading trailers can be put on hold; a failed
            inspection also holds them. Only ready or held trailers can gate out.</p>
        </section>
        <section class="card">
          <h2>TMS integration</h2>
          <p><span class="badge" [attr.data-tone]="meta.integration.configured ? 'good' : 'neutral'">{{ meta.integration.mode }}</span> {{ meta.integration.provider }}</p>
          <p class="muted">Read-only. Credentials stay on the server.</p>
        </section>
        <section class="card wide">
          <h2>About this app</h2>
          <p class="muted">A clean-room portfolio yard management app built with .NET 10, EF Core, PostgreSQL and Angular 22, hosted on Cloudflare Workers and
            Containers. All data is fictional. It is not modelled on any employer's product and contains no employer code, data or screens.</p>
        </section>
      </div>
    } @else if (session.metaError()) {
      <div class="alert" role="alert">{{ session.metaError() }}</div>
    } @else { <div class="loading" role="status">Loading…</div> }`
})
export class SettingsPage {
  readonly session = inject(Session);
}
