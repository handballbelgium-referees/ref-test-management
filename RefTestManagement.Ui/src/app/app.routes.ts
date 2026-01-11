import { Routes } from '@angular/router';
import { authGuard } from './auth/guards/auth-guard';
import { canDeactivateRefTestGuard } from './ref-test/take/guards/can-deactivate-ref-test.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  {
    path: 'ref-tests',
    loadComponent: () => import('./ref-tests/list/list-ref-tests').then((m) => m.ListRefTests),
    canActivate: [authGuard],
  },
  {
    path: 'ref-tests/create',
    loadComponent: () =>
      import('./ref-tests/create/create-ref-tests').then((m) => m.CreateRefTests),
    canActivate: [authGuard],
  },
  {
    path: 'ref-test/:token',
    loadComponent: () =>
      import('./ref-test/welcome/ref-test-welcome').then((m) => m.RefTestWelcome),
  },
  {
    path: 'ref-test/:token/take',
    loadComponent: () => import('./ref-test/take/take-ref-test').then((m) => m.TakeRefTest),
    canDeactivate: [canDeactivateRefTestGuard],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
