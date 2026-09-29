import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../core/api';
import { dateTime } from '../core/format';
import { Inspection } from '../core/models';
import { load } from '../shared/load';
import { State } from '../shared/state';

@Component({
  selector: 'app-inspections',
  imports: [RouterLink, State],
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Operations</p><h1>Inspections</h1>
        <p class="muted">Dock inspections. Every checklist item must pass for a trailer to be Ready; failures put it on hold. Start one from a trailer at a door.</p></div>
      <div class="head-actions"><a class="button ghost" routerLink="/trailers">Find a trailer</a></div>
    </header>
    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as list) {
      <div class="table-wrap">
        <table>
          <thead><tr><th scope="col">When</th><th scope="col">Trailer</th><th scope="col">Result</th><th scope="col">Checklist</th><th scope="col">Notes</th></tr></thead>
          <tbody>
            @for (i of list; track i.id) {
              <tr>
                <td class="nowrap">{{ dateTime(i.occurredAt) }}</td>
                <td><a [routerLink]="['/trailers', i.trailerNumber]">{{ i.trailerNumber }}</a></td>
                <td><span class="badge" [attr.data-tone]="i.passed ? 'good' : 'bad'">{{ i.passed ? 'Passed' : 'Failed' }}</span></td>
                <td><div class="checklist">
                  @for (item of items(i); track item.label) { <span class="badge" [attr.data-tone]="item.ok ? 'good' : 'bad'">{{ item.ok ? '✓' : '✗' }} {{ item.label }}</span> }
                </div></td>
                <td>{{ i.notes ?? '—' }}</td>
              </tr>
            } @empty { <tr><td colspan="5" class="empty-row">No inspections yet.</td></tr> }
          </tbody>
        </table>
      </div>
    }`
})
export class InspectionsPage {
  private readonly api = inject(Api);
  readonly data = load(() => this.api.inspections(100));
  readonly dateTime = dateTime;
  items = (i: Inspection) => [
    { label: 'Tires', ok: i.tires }, { label: 'Lights', ok: i.lights }, { label: 'Doors & seal', ok: i.doorsAndSeal },
    { label: 'Floor', ok: i.floor }, ...(i.reeferUnit === null ? [] : [{ label: 'Reefer', ok: i.reeferUnit }])
  ].filter(x => x.ok !== null);
}
