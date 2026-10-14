import { Routes } from '@angular/router';
import { userGuard } from './core/session/user.guard';
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
    // Only a known user may open a game; anyone else lands on the landing page.
    canActivate: [userGuard],
    loadComponent: () => import('./games/game-page/game-page').then((m) => m.GamePage),
  },
  {
    // The help section is public: no guard, and its pages never call the backend.
    path: 'help',
    loadChildren: () => import('./help/help-routes').then((m) => m.helpRoutes),
  },
  { path: '**', redirectTo: '' },
];
