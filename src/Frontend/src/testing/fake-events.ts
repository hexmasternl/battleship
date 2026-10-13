// Test double shared by the spec files; not imported by application code.
import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { EventsService } from '../app/events/events.service';
import { EventsConnectionState, RealtimeEvent } from '../app/events/realtime-event';
import { RealtimeEventType } from '../app/events/realtime-event-types';

/** Stands in for `EventsService`: tests push events and move the connection state by hand. */
export class FakeEventsService {
  readonly connected: string[] = [];
  readonly #events = new Subject<RealtimeEvent>();
  readonly #state = signal<EventsConnectionState>('disconnected');

  readonly events$ = this.#events.asObservable();
  readonly state = this.#state.asReadonly();

  connect(userId: string): void {
    this.connected.push(userId);
  }

  emit(type: RealtimeEventType, payload: unknown): void {
    this.#events.next({ type, id: '1', payload });
  }

  setState(state: EventsConnectionState): void {
    this.#state.set(state);
  }
}

export function provideFakeEvents(fake = new FakeEventsService()) {
  return { provide: EventsService, useValue: fake };
}
