import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

type Asset = { trailerNumber: string; equipment: string; palletCapacity: number; status: string; spot: string; currentLoadNumber?: string; updatedAt: string };
type Candidate = { orderId: string; customer: string; origin: string; destination: string; pallets: number; weight: number; equipment: string; reason: string };
type Outbox = { id: string; event: { eventType: string; trailerNumber: string }; createdAt: string; attempts: number; sentAt?: string; lastError?: string };

type ExternalTrailerResult = { provider: string; live: boolean; degraded: boolean; degradedReason?: string; trailers: unknown[] };

@Component({ selector: 'app-root', standalone: true, templateUrl: './app.component.html' })
export class AppComponent implements OnInit {
  private readonly http = inject(HttpClient);
  readonly assets = signal<Asset[]>([]);
  readonly candidates = signal<Candidate[]>([]);
  readonly selectedTrailer = signal<string | null>(null);
  readonly outbox = signal<Outbox[]>([]);
  readonly integrationError = signal<string | null>(null);
  readonly external = signal<ExternalTrailerResult | null>(null);

  async ngOnInit(): Promise<void> {
    await Promise.all([this.refresh(), this.refreshExternal()]);
  }

  async refresh(): Promise<void> {
    const [assets, outbox] = await Promise.all([
      firstValueFrom(this.http.get<Asset[]>('/api/assets')),
      firstValueFrom(this.http.get<Outbox[]>('/api/outbox'))
    ]);
    this.assets.set(assets);
    this.outbox.set(outbox);
  }

  async showCandidates(asset: Asset): Promise<void> {
    this.selectedTrailer.set(asset.trailerNumber);
    this.integrationError.set(null);
    try {
      this.candidates.set(await firstValueFrom(this.http.get<Candidate[]>(`/api/ltl/candidates/${encodeURIComponent(asset.trailerNumber)}`)));
    } catch {
      this.candidates.set([]);
      this.integrationError.set('LTL Planner is unavailable. Yard state was not changed.');
    }
  }

  async markReady(asset: Asset): Promise<void> {
    await firstValueFrom(this.http.post('/api/inspections', {
      trailerNumber: asset.trailerNumber,
      passed: true,
      notes: 'Portfolio demo inspection passed.'
    }));
    await this.refresh();
  }

  async gate(asset: Asset, direction: 'In' | 'Out'): Promise<void> {
    await firstValueFrom(this.http.post('/api/gate', {
      trailerNumber: asset.trailerNumber,
      direction,
      note: `Portfolio demo gate ${direction.toLowerCase()}.`
    }));
    await this.refresh();
  }

  async refreshExternal(): Promise<void> {
    this.external.set(await firstValueFrom(this.http.get<ExternalTrailerResult>('/api/alvys/trailers')));
  }
}
