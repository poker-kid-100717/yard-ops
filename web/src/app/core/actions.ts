import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Api } from './api';
import { Field, Forms, Option, options } from './form';
import { Meta, Trailer } from './models';
import { Session } from './session';

type Values = Record<string, unknown>;
const text = (v: unknown) => (typeof v === 'string' && v.trim() !== '' ? v.trim() : null);
const number = (v: unknown) => (v === null || v === '' || v === undefined ? 0 : Number(v));

/** Every yard action as a drawer form, shared by pages and the "+ New" menu. */
@Injectable({ providedIn: 'root' })
export class Actions {
  private readonly api = inject(Api);
  private readonly forms = inject(Forms);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  private get meta(): Meta {
    const meta = this.session.meta();
    if (!meta) throw new Error('Settings are still loading.');
    return meta;
  }

  private done = (navigateTo?: (result: unknown) => string | null) => (result: unknown) => {
    this.session.changed();
    const url = navigateTo?.(result);
    if (url) void this.router.navigateByUrl(url);
  };

  private async freeSpots(kind?: 'Parking' | 'Door'): Promise<Option[]> {
    const spots = await this.api.spots();
    return spots.filter(s => !s.trailer && (!kind || s.kind === kind)).map(s => ({ value: s.code, label: `${s.code} · ${s.kind === 'Door' ? 'dock door' : 'parking'}` }));
  }

  /** Gate in: pick an expected trailer or type a new one; new trailers also need equipment and capacity. */
  gateIn(trailerNumber?: string): void {
    void this.forms.openAsync(async () => {
      const [spots, expected] = await Promise.all([this.freeSpots('Parking'), this.api.trailers({ status: 'Expected', pageSize: 100 })]);
      const known = new Set(expected.items.map(t => t.trailerNumber));
      const isNew = (v: Values) => !known.has(String(v['trailerNumber'] ?? '').trim().toUpperCase());
      return {
        title: 'Gate in',
        description: expected.items.length ? `Expected: ${[...known].join(', ')}` : 'No trailers are pre-advised; enter the trailer at the gate.',
        fields: [
          { key: 'trailerNumber', label: 'Trailer number', type: 'text', required: true, placeholder: 'TRL-0000' },
          { key: 'spot', label: 'Park at', type: 'select', options: [{ value: '', label: 'First free spot' }, ...spots] },
          { key: 'equipment', label: 'Equipment', type: 'select', required: true, options: options(this.meta.equipmentTypes), when: isNew },
          { key: 'palletCapacity', label: 'Pallet capacity', type: 'number', required: true, min: 1, max: 30, when: isNew },
          { key: 'loadNumber', label: 'Load number', type: 'text' },
          { key: 'carrier', label: 'Carrier', type: 'text', when: isNew },
          { key: 'note', label: 'Gate note', type: 'textarea', wide: true }
        ],
        initial: { trailerNumber: trailerNumber ?? '', equipment: 'Dry Van', palletCapacity: 26, spot: '' },
        submitLabel: 'Record gate in',
        submit: v => this.api.post('/api/gate', {
          trailerNumber: text(v['trailerNumber']), direction: 'In', note: text(v['note']), spot: text(v['spot']),
          equipment: isNew(v) ? v['equipment'] : null, palletCapacity: isNew(v) ? number(v['palletCapacity']) : null,
          loadNumber: text(v['loadNumber']), carrier: text(v['carrier'])
        }),
        saved: this.done(r => `/trailers/${(r as { trailerNumber: string }).trailerNumber}`)
      };
    });
  }

  gateOut(trailer?: Trailer): void {
    void this.forms.openAsync(async () => {
      const ready = (await this.api.trailers({ pageSize: 100 })).items.filter(t => t.actions.includes('gate-out'));
      return {
        title: 'Gate out',
        description: 'Only trailers that are ready or on hold can leave.',
        fields: [
          { key: 'trailerNumber', label: 'Trailer', type: 'select', required: true,
            options: ready.map(t => ({ value: t.trailerNumber, label: `${t.trailerNumber} · ${t.status === 'OnHold' ? 'on hold' : 'ready'} · ${t.spot ?? ''}` })) },
          { key: 'note', label: 'Gate note', type: 'textarea', wide: true }
        ],
        initial: { trailerNumber: trailer?.trailerNumber ?? ready[0]?.trailerNumber ?? '' },
        submitLabel: 'Record gate out',
        submit: v => this.api.post('/api/gate', { trailerNumber: v['trailerNumber'], direction: 'Out', note: text(v['note']) }),
        saved: this.done()
      };
    });
  }

  expectTrailer(): void {
    this.forms.open({
      title: 'Pre-advise a trailer',
      description: 'Adds an expected arrival so the gate can check it in.',
      fields: this.trailerFields(true),
      initial: { equipment: 'Dry Van', palletCapacity: 26 },
      submitLabel: 'Add expected trailer',
      submit: v => this.api.post('/api/trailers', this.trailerBody(v)),
      saved: this.done(() => '/trailers')
    });
  }

  editTrailer(t: Trailer): void {
    this.forms.open({
      title: `Edit ${t.trailerNumber}`,
      fields: this.trailerFields(false),
      initial: { ...t, loadNumber: t.currentLoadNumber ?? '', carrier: t.carrier ?? '' },
      submitLabel: 'Save trailer',
      submit: v => this.api.put(`/api/trailers/${t.trailerNumber}`, this.trailerBody(v)),
      saved: this.done()
    });
  }

  private trailerFields(withNumber: boolean): Field[] {
    const fields: Field[] = [
      { key: 'equipment', label: 'Equipment', type: 'select', required: true, options: options(this.meta.equipmentTypes) },
      { key: 'palletCapacity', label: 'Pallet capacity', type: 'number', required: true, min: 1, max: 30 },
      { key: 'loadNumber', label: 'Load number', type: 'text' },
      { key: 'carrier', label: 'Carrier', type: 'text' }
    ];
    return withNumber ? [{ key: 'trailerNumber', label: 'Trailer number', type: 'text', required: true, placeholder: 'TRL-0000', wide: true }, ...fields] : fields;
  }

  private trailerBody = (v: Values) => ({
    trailerNumber: text(v['trailerNumber']), equipment: v['equipment'], palletCapacity: number(v['palletCapacity']),
    loadNumber: text(v['loadNumber']), carrier: text(v['carrier'])
  });

  move(t: Trailer): void {
    void this.forms.openAsync(async () => {
      const spots = await this.freeSpots();
      return {
        title: `Move ${t.trailerNumber}`,
        description: `Now at ${t.spot ?? 'no spot'}. Moving to a dock door puts an arrived trailer at the door.`,
        fields: [{ key: 'spot', label: 'Move to', type: 'select', required: true, options: spots, wide: true }],
        initial: { spot: spots.find(s => s.label.includes('door'))?.value ?? spots[0]?.value ?? '' },
        submitLabel: 'Move trailer',
        submit: v => this.api.post(`/api/trailers/${t.trailerNumber}/move`, { spot: v['spot'] }),
        saved: this.done()
      };
    });
  }

  hold(t: Trailer): void {
    this.forms.open({
      title: `Hold ${t.trailerNumber}`,
      fields: [{ key: 'reason', label: 'Reason', type: 'textarea', required: true, wide: true, placeholder: 'What is blocking this trailer?' }],
      submitLabel: 'Place on hold',
      submit: v => this.api.post(`/api/trailers/${t.trailerNumber}/hold`, { reason: text(v['reason']) }),
      saved: this.done()
    });
  }

  inspect(t: Trailer): void {
    const reefer = t.equipment === 'Reefer';
    const fields: Field[] = [
      { key: 'tires', label: 'Tires and wheels OK', type: 'checkbox', wide: true },
      { key: 'lights', label: 'Lights and reflectors OK', type: 'checkbox', wide: true },
      { key: 'doorsAndSeal', label: 'Doors close and seal applied', type: 'checkbox', wide: true },
      { key: 'floor', label: 'Floor clean and sound', type: 'checkbox', wide: true },
      { key: 'notes', label: 'Notes', type: 'textarea', wide: true, placeholder: 'Seal number, damage, anything the next shift should know' }
    ];
    if (reefer) fields.splice(4, 0, { key: 'reeferUnit', label: 'Reefer unit running at set point', type: 'checkbox', wide: true });
    this.forms.open({
      title: `Inspect ${t.trailerNumber}`,
      description: 'Every item must pass for the trailer to be Ready; a failed inspection puts it on hold. Passing sends a ready event to LTL Planner.',
      fields,
      initial: { tires: false, lights: false, doorsAndSeal: false, floor: false, reeferUnit: false },
      submitLabel: 'Record inspection',
      submit: v => this.api.post('/api/inspections', {
        trailerNumber: t.trailerNumber, passed: false, notes: text(v['notes']),
        tires: v['tires'] === true, lights: v['lights'] === true, doorsAndSeal: v['doorsAndSeal'] === true, floor: v['floor'] === true,
        reeferUnit: reefer ? v['reeferUnit'] === true : null
      }),
      saved: this.done()
    });
  }

  async act(t: Trailer, action: 'start-loading' | 'release'): Promise<void> {
    await this.api.post(`/api/trailers/${t.trailerNumber}/${action}`);
    this.session.changed();
  }
}
