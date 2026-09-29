import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, ApiError } from '../core/api';
import { dateTime } from '../core/format';
import { Session } from '../core/session';
import { load } from '../shared/load';
import { State } from '../shared/state';

@Component({
  selector: 'app-outbox',
  imports: [RouterLink, State],
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Integration</p><h1>LTL Outbox</h1>
        <p class="muted">Events for LTL Planner are saved with the yard change that caused them, then delivered with an HMAC-SHA256 signature.
          If LTL is down they wait and retry with growing delays; yard work never waits on LTL.</p></div>
      <div class="head-actions"><button type="button" (click)="drain()" [disabled]="draining()">{{ draining() ? 'Delivering…' : 'Deliver now' }}</button></div>
    </header>
    <div class="tabs" role="tablist" aria-label="Delivery state">
      @for (s of states; track s.value) {
        <button type="button" role="tab" [attr.aria-selected]="state() === s.value" [class.active]="state() === s.value" (click)="state.set(s.value)">{{ s.label }}</button>
      }
    </div>
    @if (result()) { <p class="muted" role="status">{{ result() }}</p> }
    @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }
    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as list) {
      <div class="table-wrap">
        <table>
          <thead><tr><th scope="col">Created</th><th scope="col">Event</th><th scope="col">Trailer</th><th scope="col">State</th>
            <th scope="col" class="num">Attempts</th><th scope="col">Next try / delivered</th><th scope="col">Last error</th></tr></thead>
          <tbody>
            @for (m of list; track m.id) {
              <tr>
                <td class="nowrap">{{ dateTime(m.createdAt) }}</td><td>{{ m.event.eventType }}</td>
                <td><a [routerLink]="['/trailers', m.event.trailerNumber]">{{ m.event.trailerNumber }}</a></td>
                <td><span class="badge" [attr.data-tone]="m.sentAt ? 'good' : m.attempts ? 'bad' : 'warn'">{{ m.sentAt ? 'Delivered' : m.attempts ? 'Retrying' : 'Pending' }}</span></td>
                <td class="num">{{ m.attempts }}</td>
                <td class="nowrap">{{ m.sentAt ? dateTime(m.sentAt) : dateTime(m.nextAttemptAt) }}</td>
                <td class="small">{{ m.lastError ?? '—' }}</td>
              </tr>
            } @empty { <tr><td colspan="7" class="empty-row">Nothing here.</td></tr> }
          </tbody>
        </table>
      </div>
    }`
})
export class OutboxPage {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  readonly states = [{ value: '', label: 'All' }, { value: 'pending', label: 'Pending' }, { value: 'failing', label: 'Retrying' }, { value: 'delivered', label: 'Delivered' }];
  readonly state = signal('');
  readonly draining = signal(false);
  readonly result = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly data = load(() => this.api.outbox(this.state()));
  readonly dateTime = dateTime;

  async drain(): Promise<void> {
    this.draining.set(true);
    this.error.set(null);
    try {
      const r = await this.api.drain();
      this.result.set(r.attempted === 0 ? `Nothing due right now; ${r.stillPending} pending.` : `Delivered ${r.delivered} of ${r.attempted}; ${r.stillPending} still pending.`);
      this.session.changed();
    } catch (e) { this.error.set(ApiError.from(e).message); } finally { this.draining.set(false); }
  }
}
