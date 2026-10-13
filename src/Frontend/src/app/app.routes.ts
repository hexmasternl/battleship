import { Routes } from '@angular/router';
import { isGameCode } from './games/game-code';

// Every feature route is lazy so the landing page is its own chunk and the game screens, once
// they exist, never load before a visitor asks for them.
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Battleship',
    loadComponent: () => import('./welcome/welcome-page').then((m) => m.WelcomePage),
  },
  {
    path: 'games/:gameCode',
    // Only a well-formed code matches, so retired links such as /games/new and /games/join fall to the wildcard.
    canMatch: [(_route, segments) => isGameCode(segments[1]?.path ?? '')],
    title: (route) => `Game ${route.paramMap.get('gameCode')} | Battleship`,
    loadComponent: () => import('./games/game-placeholder').then((m) => m.GamePlaceholder),
  },
  { path: '**', redirectTo: '' },
];
