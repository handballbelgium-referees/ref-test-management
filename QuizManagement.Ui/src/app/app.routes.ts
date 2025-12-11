import { Routes } from '@angular/router';
import { authGuard } from './auth/guards/auth-guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  {
    path: 'sessions/create',
    loadComponent: () => import('./sessions/create/create-sessions').then((m) => m.CreateSessions),
    canActivate: [authGuard],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
