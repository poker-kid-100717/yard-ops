import { Component, inject } from '@angular/core';
import { Api } from '../core/api';
import { load } from '../shared/load';
import { State } from '../shared/state';

@Component({
  selector: 'app-tms',
  imports: [State],
  template: `
    <header class="page-head">
      <div><p class="eyebrow">Integration</p><h1>TMS Trailers</h1>
        <p class="muted">Trailers from the transportation-management system, read through a server-side adapter. Read-only.</p></div>
      <div class="head-actions"><button type="button" class="ghost" (click)="data.reload()" [disabled]="data.loading()">Refresh</button></div>
    </header>
    <app-state [loading]="data.loading()" [error]="data.error()" [hasValue]="!!data.value()" (retry)="data.reload()" />
    @if (data.value(); as r) {
      <section class="card integration-card">
        <div><h2>{{ r.live ? 'Live read from ' + r.provider : 'Demo data' }}</h2>
          <p class="muted">{{ r.live ? 'Credentials and tokens stay on the server.' : 'No TMS credentials are configured, so this shows synthetic trailers.' }}</p></div>
        <span class="badge" [attr.data-tone]="r.live ? 'good' : 'neutral'">{{ r.live ? 'Live' : 'Demo' }}</span>
      </section>
      @if (r.degraded) { <div class="alert" role="alert">{{ r.degradedReason }}</div> }
      <div class="table-wrap">
        <table>
          <thead><tr><th scope="col">Trailer</th><th scope="col">Status</th><th scope="col">Type</th><th scope="col">Size</th><th scope="col">Fleet</th></tr></thead>
          <tbody>
            @for (t of r.trailers; track t.trailerNumber) {
              <tr><td><strong>{{ t.trailerNumber }}</strong></td><td>{{ t.status }}</td><td>{{ t.equipmentType ?? '—' }}</td>
                <td>{{ t.equipmentSize ?? '—' }}</td><td>{{ t.fleetName ?? '—' }}</td></tr>
            } @empty { <tr><td colspan="5" class="empty-row">No trailers returned.</td></tr> }
          </tbody>
        </table>
      </div>
    }`
})
export class TmsPage {
  private readonly api = inject(Api);
  readonly data = load(() => this.api.tmsTrailers());
}
