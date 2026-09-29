import { Component, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, ApiError } from '../core/api';
import { dateTime, num, statusLabel, tone } from '../core/format';
import { LtlCandidate } from '../core/models';
import { load } from '../shared/load';
import { State } from '../shared/state';
import { TrailerActions } from '../shared/trailer-actions';

@Component({
  selector: 'app-trailer-detail',
  imports: [RouterLink, State, TrailerActions],
  template: `
    <nav class="breadcrumbs" aria-label="Breadcrumb"><a routerLink="/trailers">Trailers</a><span aria-hidden="true">/</span><span aria-current="page">{{ number() }}</span></nav>
    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as d) {
      <section class="card account-header">
        <div class="account-title">
          <div><h1>{{ d.trailer.trailerNumber }}</h1>
            <p class="muted">{{ d.trailer.equipment }} · {{ d.trailer.palletCapacity }} pallets @if (d.trailer.carrier) { · {{ d.trailer.carrier }} }</p></div>
          <span class="badge large" [attr.data-tone]="tone(d.trailer.status)">{{ statusLabel(d.trailer.status) }}</span>
        </div>
        <dl class="kpis">
          <div><dt>Spot</dt><dd>{{ d.trailer.spot ?? '—' }} @if (d.trailer.spotKind === 'Door') { <span class="small muted">door</span> }</dd></div>
          <div><dt>Load</dt><dd>{{ d.trailer.currentLoadNumber ?? '—' }}</dd></div>
          <div><dt>Updated</dt><dd class="small">{{ dateTime(d.trailer.updatedAt) }}</dd></div>
        </dl>
        @if (d.trailer.holdReason) { <p class="alert" role="status">On hold: {{ d.trailer.holdReason }}</p> }
        <app-trailer-actions [trailer]="d.trailer" />
      </section>

      <section class="card">
        <div class="card-head"><div><h2>LTL candidates</h2><p class="muted">Open orders LTL Planner says fit this trailer's equipment and capacity.</p></div>
          <button type="button" class="ghost small" (click)="lookUp()" [disabled]="looking()">{{ looking() ? 'Asking LTL…' : 'Ask LTL Planner' }}</button></div>
        @if (candidateError()) { <div class="alert" role="alert">{{ candidateError() }}</div> }
        @if (candidates(); as list) {
          <ul class="compact-list">
            @for (c of list; track c.orderId) {
              <li><strong>{{ c.orderId }}</strong><div>{{ c.customer }}<div class="muted small">{{ c.origin }} → {{ c.destination }} · {{ c.pallets }} pallets · {{ num(c.weight) }} lb</div></div></li>
            } @empty { <li class="empty-row">No open orders fit this trailer right now.</li> }
          </ul>
        }
      </section>

      <div class="two-col">
        <section class="card">
          <h2>History</h2>
          <ul class="timeline">
            @for (e of history(d); track e.id) {
              <li><span class="badge" [attr.data-tone]="e.tone">{{ e.kind }}</span><div><p>{{ e.text }}</p><p class="muted small">{{ dateTime(e.at) }}</p></div></li>
            } @empty { <li class="empty-row">No history yet.</li> }
          </ul>
        </section>
        <section class="card">
          <h2>Events sent to LTL</h2>
          <ul class="timeline">
            @for (m of d.outbox; track m.id) {
              <li><span class="badge" [attr.data-tone]="m.sentAt ? 'good' : m.attempts ? 'bad' : 'warn'">{{ m.sentAt ? 'Delivered' : m.attempts ? 'Retrying' : 'Pending' }}</span>
                <div><p>{{ m.event.eventType }}</p><p class="muted small">{{ dateTime(m.createdAt) }} · {{ m.attempts }} attempt{{ m.attempts === 1 ? '' : 's' }}
                  @if (m.lastError) { · {{ m.lastError }} }</p></div></li>
            } @empty { <li class="empty-row">No events yet.</li> }
          </ul>
        </section>
      </div>
    }`
})
export class TrailerDetailPage {
  private readonly api = inject(Api);
  readonly number = input.required<string>();
  readonly data = load(() => this.api.trailer(this.number()));
  readonly candidates = signal<LtlCandidate[] | null>(null);
  readonly candidateError = signal<string | null>(null);
  readonly looking = signal(false);
  readonly dateTime = dateTime;
  readonly num = num;
  readonly tone = tone;
  readonly statusLabel = statusLabel;

  history(d: NonNullable<ReturnType<typeof this.data.value>>) {
    return [
      ...d.gate.map(g => ({ id: g.id, kind: `Gate ${g.direction.toLowerCase()}`, tone: 'neutral', at: g.occurredAt, text: g.note ?? '' })),
      ...d.moves.map(m => ({ id: m.id, kind: 'Move', tone: 'neutral', at: m.occurredAt, text: `${m.fromSpot ?? 'Gate'} → ${m.toSpot}` })),
      ...d.inspections.map(i => ({ id: i.id, kind: 'Inspection', tone: i.passed ? 'good' : 'bad', at: i.occurredAt,
        text: `${i.passed ? 'Passed' : 'Failed'}${i.notes ? ': ' + i.notes : ''}` }))
    ].sort((a, b) => b.at.localeCompare(a.at));
  }

  async lookUp(): Promise<void> {
    this.looking.set(true);
    this.candidateError.set(null);
    try { this.candidates.set(await this.api.candidates(this.number())); }
    catch (e) { this.candidateError.set(ApiError.from(e).message); }
    finally { this.looking.set(false); }
  }
}
