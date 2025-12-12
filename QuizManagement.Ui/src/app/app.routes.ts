import { Routes } from '@angular/router';
import { authGuard } from './auth/guards/auth-guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  {
    path: 'sessions',
    loadComponent: () => import('./sessions/list/list-sessions').then((m) => m.ListSessions),
    canActivate: [authGuard],
  },
  {
    path: 'sessions/create',
    loadComponent: () => import('./sessions/create/create-sessions').then((m) => m.CreateSessions),
    canActivate: [authGuard],
  },
  {
    path: 'quiz/:token',
    loadComponent: () => import('./quiz/welcome/quiz-welcome').then((m) => m.QuizWelcomeComponent),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
