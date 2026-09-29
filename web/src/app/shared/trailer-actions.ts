import { Component, inject, input, signal } from '@angular/core';
import { Actions } from '../core/actions';
import { ApiError } from '../core/api';
import { Trailer } from '../core/models';

/** The buttons a trailer's current status allows, driven by the same rules the API enforces. */
@Component({
  selector: 'app-trailer-actions',
  template: `
    <div class="button-row">
      @for (a of trailer().actions; track a) {
        @switch (a) {
          @case ('gate-in') { <button type="button" (click)="actions.gateIn(trailer().trailerNumber)">Gate in</button> }
          @case ('gate-out') { <button type="button" (click)="actions.gateOut(trailer())">Gate out</button> }
          @case ('start-loading') { <button type="button" (click)="run('start-loading')">Start loading</button> }
          @case ('inspect') { <button type="button" (click)="actions.inspect(trailer())">Inspect</button> }
          @case ('release') { <button type="button" (click)="run('release')">Release hold</button> }
          @case ('hold') { <button type="button" class="ghost" (click)="actions.hold(trailer())">Hold</button> }
        }
      }
      @if (trailer().actions.includes('move-to-door') || trailer().actions.includes('move-to-parking')) {
        <button type="button" class="ghost" (click)="actions.move(trailer())">Move</button>
      }
      @if (trailer().status !== 'Departed') { <button type="button" class="ghost" (click)="actions.editTrailer(trailer())">Edit</button> }
    </div>
    @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }`
})
export class TrailerActions {
  readonly actions = inject(Actions);
  readonly trailer = input.required<Trailer>();
  readonly error = signal<string | null>(null);

  async run(action: 'start-loading' | 'release'): Promise<void> {
    this.error.set(null);
    try { await this.actions.act(this.trailer(), action); } catch (e) { this.error.set(ApiError.from(e).message); }
  }
}
