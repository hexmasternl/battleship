import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { UserSession } from './user-session';
import { userGuard } from './user.guard';

describe('userGuard', () => {
  function run(known: boolean): Promise<unknown> {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: UserSession, useValue: { ensureUser: () => Promise.resolve(known) } },
      ],
    });
    return runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
      Promise.resolve(
        userGuard({} as ActivatedRouteSnapshot, { url: '/games/K7Q2M9XA' } as RouterStateSnapshot),
      ),
    );
  }

  it('lets a known user through', async () => {
    expect(await run(true)).toBe(true);
  });

  it('sends a visitor who is not a known user to the landing page', async () => {
    const result = await run(false);
    expect(result).toBeInstanceOf(UrlTree);
    expect((result as UrlTree).toString()).toBe('/');
  });
});
