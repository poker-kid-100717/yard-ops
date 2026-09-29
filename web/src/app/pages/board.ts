import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { statusLabel, tone } from '../core/format';
import { Actions } from '../core/actions';
import { load } from '../shared/load';
import { State } from '../shared/state';

@Component({
  selector: 'app-board',
  imports: [RouterLink, State],
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Overview</p><h1>Yard Board</h1><p class="muted">Every parking spot and dock door, and which trailer is in it. Tap a trailer to work it.</p></div>
      <div class="head-actions"><button type="button" (click)="actions.gateIn()">Gate in</button><button type="button" class="ghost" (click)="actions.gateOut()">Gate out</button></div>
    </header>

    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as d) {
      <section class="stat-grid" aria-label="Yard numbers">
        <div class="stat"><span>Trailers on yard</span><strong>{{ d.dashboard.onYard }}</strong><small>{{ d.dashboard.expected }} expected</small></div>
        <div class="stat"><span>Free parking</span><strong>{{ d.dashboard.parkingFree }}</strong><small>of {{ parkingTotal() }} spots</small></div>
        <div class="stat"><span>Doors in use</span><strong>{{ d.dashboard.doorsInUse }}/{{ d.dashboard.doors }}</strong><small>{{ count('Loading') }} loading</small></div>
        <a class="stat" routerLink="/trailers"><span>Ready to go</span><strong>{{ count('Ready') }}</strong><small>{{ count('OnHold') }} on hold</small></a>
        <a class="stat" routerLink="/outbox"><span>Events to LTL</span><strong>{{ d.dashboard.outboxPending }}</strong>
          <small [class.bad-text]="d.dashboard.outboxFailing > 0">{{ d.dashboard.outboxFailing ? d.dashboard.outboxFailing + ' retrying' : 'pending' }}</small></a>
        <a class="stat" routerLink="/gate"><span>Gate moves (24 h)</span><strong>{{ d.dashboard.gateInsToday + d.dashboard.gateOutsToday }}</strong>
          <small>{{ d.dashboard.gateInsToday }} in · {{ d.dashboard.gateOutsToday }} out</small></a>
      </section>

      @for (group of [{ label: 'Dock doors', kind: 'Door' }, { label: 'Parking', kind: 'Parking' }]; track group.kind) {
        <h2 class="section-label">{{ group.label }}</h2>
        <div class="yard-grid">
          @for (s of d.spots; track s.code) {
            @if (s.kind === group.kind) {
              @if (s.trailer; as t) {
                <a class="spot taken" [class.door]="s.kind === 'Door'" [routerLink]="['/trailers', t.trailerNumber]"
                   [attr.aria-label]="s.code + ': ' + t.trailerNumber + ', ' + statusLabel(t.status)">
                  <span class="spot-code">{{ s.code }}<span>{{ t.equipment }}</span></span>
                  <strong>{{ t.trailerNumber }}</strong>
                  <span class="badge" [attr.data-tone]="tone(t.status)">{{ statusLabel(t.status) }}</span>
                </a>
              } @else {
                <div class="spot" [class.door]="s.kind === 'Door'"><span class="spot-code">{{ s.code }}</span><span class="muted small">Free</span></div>
              }
            }
          }
        </div>
      }
    }`
})
export class BoardPage {
  private readonly api = inject(Api);
  readonly actions = inject(Actions);
  readonly data = load(async () => {
    const [dashboard, spots] = await Promise.all([this.api.dashboard(), this.api.spots()]);
    return { dashboard, spots };
  });
  readonly parkingTotal = computed(() => this.data.value()?.spots.filter(s => s.kind === 'Parking').length ?? 0);
  readonly tone = tone;
  readonly statusLabel = statusLabel;
  count = (status: string) => this.data.value()?.dashboard.byStatus.find(s => s.status === status)?.count ?? 0;
}
