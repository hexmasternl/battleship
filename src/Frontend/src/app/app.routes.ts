import { Routes } from '@angular/router';

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
    path: 'games/new',
    title: 'Create a new game | Battleship',
    data: { feature: 'create' },
    loadComponent: () => import('./games/coming-soon').then((m) => m.ComingSoon),
  },
  {
    path: 'games/join',
    title: 'Join an existing game | Battleship',
    data: { feature: 'join' },
    loadComponent: () => import('./games/coming-soon').then((m) => m.ComingSoon),
  },
  { path: '**', redirectTo: '' },
];
