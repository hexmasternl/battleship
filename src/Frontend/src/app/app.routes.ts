import { Routes } from '@angular/router';
import { playerGuard } from './core/session/player.guard';

/** A game code as the Games service issues it: eight upper-case letters or digits. */
export const GAME_CODE_PATTERN = /^[A-Z0-9]{8}$/;

// Every feature route is lazy so the landing page is its own chunk and the game screens, once
// they exist, never load before a visitor asks for them.
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Battleship',
    loadComponent: () => import('./welcome/welcome-page').then((m) => m.WelcomePage),
  },
  // Before `games/:gameCode`, so "join" is never read as a game code.
  {
    path: 'games/join',
    title: 'Join an existing game | Battleship',
    data: { feature: 'join' },
    loadComponent: () => import('./games/coming-soon').then((m) => m.ComingSoon),
  },
  {
    path: 'games/:gameCode',
    // Only a well-formed code matches, so retired links such as /games/new fall to the wildcard.
    canMatch: [(_route, segments) => GAME_CODE_PATTERN.test(segments[1]?.path ?? '')],
    title: (route) => `Game ${route.paramMap.get('gameCode')} | Battleship`,
    // Only a known player may open a game; anyone else lands on the landing page.
    canActivate: [playerGuard],
    loadComponent: () => import('./games/game-page/game-page').then((m) => m.GamePage),
  },
  { path: '**', redirectTo: '' },
];
