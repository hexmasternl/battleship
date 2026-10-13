import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserSession } from './user-session';

/**
 * Lets a route open only for a known user. A restore in flight (or one more restore attempt
 * with a stored refresh token) is awaited first; a visitor who is still unknown is sent to the
 * landing page, so the route's component never loads and never requests anything.
 */
export const userGuard: CanActivateFn = async () => {
  const router = inject(Router);
  return (await inject(UserSession).ensureUser()) ? true : router.parseUrl('/');
};
