import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Actions } from '../core/actions';
import { Api } from '../core/api';
import { dateTime, statusLabel, tone } from '../core/format';
import { load } from '../shared/load';
import { Pager } from '../shared/pager';
import { State } from '../shared/state';

@Component({
  selector: 'app-trailers',
  imports: [RouterLink, State, Pager],
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Operations</p><h1>Trailers</h1><p class="muted">Every trailer on or expected at the yard. Open one to move, load, inspect or release it.</p></div>
      <div class="head-actions"><button type="button" (click)="actions.expectTrailer()">Pre-advise trailer</button></div>
    </header>
    <div class="toolbar">
      <div class="tabs" role="tablist" aria-label="Trailer status">
        @for (s of statuses; track s.value) {
          <button type="button" role="tab" [attr.aria-selected]="status() === s.value" [class.active]="status() === s.value" (click)="status.set(s.value); page.set(1)">{{ s.label }}</button>
        }
      </div>
      <label class="filter"><span class="sr-only">Search trailers</span>
        <input type="search" placeholder="Search trailer, load or carrier" [value]="search()" (input)="search.set($any($event.target).value); page.set(1)" /></label>
    </div>

    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as p) {
      <div class="table-wrap">
        <table>
          <thead><tr><th scope="col">Trailer</th><th scope="col">Status</th><th scope="col">Spot</th><th scope="col">Equipment</th>
            <th scope="col">Load</th><th scope="col">Carrier</th><th scope="col">Updated</th></tr></thead>
          <tbody>
            @for (t of p.items; track t.trailerNumber) {
              <tr>
                <td><a class="strong-link" [routerLink]="['/trailers', t.trailerNumber]">{{ t.trailerNumber }}</a></td>
                <td><span class="badge" [attr.data-tone]="tone(t.status)">{{ statusLabel(t.status) }}</span>
                  @if (t.holdReason) { <div class="muted small clamp">{{ t.holdReason }}</div> }</td>
                <td>{{ t.spot ?? '—' }}</td><td>{{ t.equipment }} · {{ t.palletCapacity }}</td>
                <td>{{ t.currentLoadNumber ?? '—' }}</td><td>{{ t.carrier ?? '—' }}</td><td class="nowrap">{{ dateTime(t.updatedAt) }}</td>
              </tr>
            } @empty { <tr><td colspan="7" class="empty-row">No trailers match.</td></tr> }
          </tbody>
        </table>
      </div>
      <app-pager [page]="p.page" [pageSize]="p.pageSize" [total]="p.total" (go)="page.set($event)" />
    }`
})
export class TrailersPage {
  private readonly api = inject(Api);
  readonly actions = inject(Actions);
  readonly statuses = [
    { value: '', label: 'On yard & expected' }, { value: 'Expected', label: 'Expected' }, { value: 'AtDoor', label: 'At door' },
    { value: 'Loading', label: 'Loading' }, { value: 'Ready', label: 'Ready' }, { value: 'OnHold', label: 'On hold' },
    { value: 'Departed', label: 'Departed' }, { value: 'All', label: 'All' }
  ];
  readonly status = signal('');
  readonly search = signal('');
  readonly page = signal(1);
  readonly data = load(() => this.api.trailers({ status: this.status(), search: this.search(), page: this.page(), pageSize: 25 }));
  readonly dateTime = dateTime;
  readonly tone = tone;
  readonly statusLabel = statusLabel;
}
