import { Routes } from '@angular/router';
import { authGuard } from './auth/guards/auth-guard';
import { permissionGuard } from './auth/guards/permission-guard';
import { Permissions } from './auth/models/permissions';
import { privacyConfirmationGuard } from './privacy/guards/privacy-confirmation.guard';
import {
  refTestInvitationFragmentGuard,
  refTestSessionGuard,
  refTestTakeGuard,
} from './ref-test/guards/ref-test-token.guard';
import { refTestGuard } from './ref-test/take/guards/can-deactivate-ref-test.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./home/home').then((m) => m.Home),
  },
  {
    path: 'privacy/export-request',
    pathMatch: 'full',
    loadComponent: () =>
      import('./privacy/components/personal-data-export/request/personal-data-export-request').then(
        (m) => m.PersonalDataExportRequest,
      ),
  },
  {
    path: 'privacy/export-confirmation',
    pathMatch: 'full',
    canActivate: [privacyConfirmationGuard],
    loadComponent: () =>
      import('./privacy/components/personal-data-export/confirmation/personal-data-export-confirmation').then(
        (m) => m.PersonalDataExportConfirmation,
      ),
  },
  {
    path: 'privacy/withdrawal-request',
    pathMatch: 'full',
    loadComponent: () =>
      import('./privacy/components/privacy-withdrawal/request/privacy-withdrawal-request').then(
        (m) => m.PrivacyWithdrawalRequest,
      ),
  },
  {
    path: 'privacy/withdrawal-confirmation',
    pathMatch: 'full',
    canActivate: [privacyConfirmationGuard],
    loadComponent: () =>
      import('./privacy/components/privacy-withdrawal/confirmation/privacy-withdrawal-confirmation').then(
        (m) => m.PrivacyWithdrawalConfirmation,
      ),
  },
  {
    path: 'privacy',
    loadComponent: () => import('./privacy/privacy-notice').then((m) => m.PrivacyNotice),
  },
  {
    path: 'ref-tests',
    loadComponent: () => import('./ref-tests/list/list-ref-tests').then((m) => m.ListRefTests),
    canActivate: [authGuard, permissionGuard(Permissions.RefTests.ViewList)],
  },
  {
    path: 'ref-tests/create',
    loadComponent: () =>
      import('./ref-tests/create/create-ref-tests').then((m) => m.CreateRefTests),
    canActivate: [authGuard, permissionGuard(Permissions.RefTests.Create)],
  },
  {
    path: 'ref-tests/:id',
    loadComponent: () => import('./ref-tests/detail/ref-test-detail').then((m) => m.RefTestDetail),
    canActivate: [authGuard, permissionGuard(Permissions.RefTests.ViewDetail)],
    children: [
      {
        path: '',
        redirectTo: 'details',
        pathMatch: 'full',
      },
      {
        path: 'details',
        loadComponent: () =>
          import('./ref-tests/detail/components/ref-test-detail-tab/ref-test-detail-tab').then(
            (m) => m.RefTestDetailTab,
          ),
      },
      {
        path: 'questions',
        loadComponent: () =>
          import('./ref-tests/detail/components/ref-test-questions-tab/ref-test-questions-tab').then(
            (m) => m.RefTestQuestionsTab,
          ),
        canActivate: [permissionGuard(Permissions.RefTests.ViewDetailQuestions)],
      },
    ],
  },
  {
    path: 'audit-logs',
    loadComponent: () => import('./audit-logs/list-audit-logs').then((m) => m.ListAuditLogs),
    canActivate: [authGuard, permissionGuard(Permissions.AuditLogs.View)],
  },
  {
    path: 'ref-test',
    pathMatch: 'full',
    canActivate: [refTestInvitationFragmentGuard],
    loadComponent: () =>
      import('./ref-test/welcome/ref-test-welcome').then((m) => m.RefTestWelcome),
  },
  {
    path: 'ref-test/welcome',
    pathMatch: 'full',
    canActivate: [refTestSessionGuard],
    loadComponent: () =>
      import('./ref-test/welcome/ref-test-welcome').then((m) => m.RefTestWelcome),
  },
  {
    path: 'ref-test/take',
    pathMatch: 'full',
    canActivate: [refTestTakeGuard],
    loadComponent: () => import('./ref-test/take/take-ref-test').then((m) => m.TakeRefTest),
    canDeactivate: [refTestGuard],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
