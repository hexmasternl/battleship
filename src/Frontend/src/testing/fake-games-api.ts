// Test double shared by the spec files; not imported by application code.
import { GamesApi } from '../app/games/games-api';
import { GameView, ShipKind, ShipPlacementRequest } from '../app/games/game-view';

/** One call the page made, with the promise the test settles by hand. */
export interface PendingCall {
  readonly method: 'getGame' | 'placeShip' | 'markReady';
  readonly args: readonly unknown[];
  resolve(view: GameView): void;
  reject(error: unknown): void;
}

/** Stands in for `GamesApi`: every call waits until the test resolves or rejects it. */
export class FakeGamesApi {
  readonly calls: PendingCall[] = [];

  getGame(code: string): Promise<GameView> {
    return this.#call('getGame', [code]);
  }

  placeShip(code: string, kind: ShipKind, placement: ShipPlacementRequest): Promise<GameView> {
    return this.#call('placeShip', [code, kind, placement]);
  }

  markReady(code: string): Promise<GameView> {
    return this.#call('markReady', [code]);
  }

  /** The calls of one method, in the order they were made. */
  of(method: PendingCall['method']): PendingCall[] {
    return this.calls.filter((call) => call.method === method);
  }

  /** The most recent call of one method. */
  last(method: PendingCall['method']): PendingCall {
    const call = this.of(method).at(-1);
    if (call === undefined) throw new Error(`No ${method} call was made`);
    return call;
  }

  #call(method: PendingCall['method'], args: readonly unknown[]): Promise<GameView> {
    return new Promise<GameView>((resolve, reject) => {
      this.calls.push({ method, args, resolve, reject });
    });
  }
}

export function provideFakeGamesApi(fake = new FakeGamesApi()) {
  return { provide: GamesApi, useValue: fake };
}

/** Lets resolved promises and their continuations run. */
export function flushPromises(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}
