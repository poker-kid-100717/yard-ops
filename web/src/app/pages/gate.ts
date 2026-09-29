import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Actions } from '../core/actions';
import { Api } from '../core/api';
import { dateTime } from '../core/format';
import { load } from '../shared/load';
import { State } from '../shared/state';

@Component({
  selector: 'app-gate',
  imports: [RouterLink, State],
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Operations</p><h1>Gate</h1>
        <p class="muted">Check trailers in and out. Each gate move is recorded here and sent to LTL Planner as a signed event.</p></div>
      <div class="head-actions">
        <button type="button" (click)="actions.gateIn()">Gate in</button>
        <button type="button" class="ghost" (click)="actions.gateOut()">Gate out</button>
        <button type="button" class="ghost" (click)="actions.expectTrailer()">Pre-advise trailer</button>
      </div>
    </header>

    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as d) {
      @if (d.expected.length) {
        <section class="card">
          <h2>Expected arrivals</h2>
          <ul class="compact-list">
            @for (t of d.expected; track t.trailerNumber) {
              <li><strong>{{ t.trailerNumber }}</strong><div>{{ t.equipment }} · {{ t.palletCapacity }} pallets @if (t.carrier) { · {{ t.carrier }} }
                @if (t.currentLoadNumber) { · {{ t.currentLoadNumber }} }</div>
                <button type="button" class="small" (click)="actions.gateIn(t.trailerNumber)">Gate in</button></li>
            }
          </ul>
        </section>
      }
      <div class="table-wrap">
        <table>
          <thead><tr><th scope="col">When</th><th scope="col">Trailer</th><th scope="col">Direction</th><th scope="col">Note</th></tr></thead>
          <tbody>
            @for (g of d.history; track g.id) {
              <tr><td class="nowrap">{{ dateTime(g.occurredAt) }}</td><td><a [routerLink]="['/trailers', g.trailerNumber]">{{ g.trailerNumber }}</a></td>
                <td><span class="badge" [attr.data-tone]="g.direction === 'In' ? 'good' : 'neutral'">{{ g.direction }}</span></td><td>{{ g.note ?? '—' }}</td></tr>
            } @empty { <tr><td colspan="4" class="empty-row">No gate activity yet.</td></tr> }
          </tbody>
        </table>
      </div>
    }`
})
export class GatePage {
  private readonly api = inject(Api);
  readonly actions = inject(Actions);
  readonly data = load(async () => {
    const [history, expected] = await Promise.all([this.api.gateHistory(50), this.api.trailers({ status: 'Expected', pageSize: 100 })]);
    return { history, expected: expected.items };
  });
  readonly dateTime = dateTime;
}
