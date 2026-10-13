import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PlayerSession } from './player-session';

/**
 * Lets a route open only for a known player. A restore in flight (or one more restore attempt
 * with a stored refresh token) is awaited first; a visitor who is still unknown is sent to the
 * landing page, so the route's component never loads and never requests anything.
 */
export const playerGuard: CanActivateFn = async () => {
  const router = inject(Router);
  return (await inject(PlayerSession).ensurePlayer()) ? true : router.parseUrl('/');
};
