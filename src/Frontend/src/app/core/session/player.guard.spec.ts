import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { PlayerSession } from './player-session';
import { playerGuard } from './player.guard';

describe('playerGuard', () => {
  function run(known: boolean): Promise<unknown> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: PlayerSession, useValue: { ensurePlayer: () => Promise.resolve(known) } },
      ],
    });
    return runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
      Promise.resolve(
        playerGuard(
          {} as ActivatedRouteSnapshot,
          { url: '/games/K7Q2M9XA' } as RouterStateSnapshot,
        ),
      ),
    );
  }

  it('lets a known player through', async () => {
    expect(await run(true)).toBe(true);
  });

  it('sends a visitor without a player to the landing page', async () => {
    const result = await run(false);
    expect(result).toBeInstanceOf(UrlTree);
    expect((result as UrlTree).toString()).toBe('/');
  });
});
