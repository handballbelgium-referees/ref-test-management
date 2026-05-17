import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ActivatedRoute,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, EMPTY, filter, map, tap } from 'rxjs';
import { RefTestStatus } from '../../../../graphql/generated';
import { HasPermission } from '../../auth/directives/has-permission.directive';
import { Permissions } from '../../auth/models/permissions';
import { Banner } from '../../shared/components/banner/banner';
import { ApproveRefTestsDialog } from '../list/components/dialogs/approve-ref-tests-dialog/approve-ref-tests-dialog';
import { DeleteRefTestsDialog } from '../list/components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog';
import { RejectRefTestsDialog } from '../list/components/dialogs/reject-ref-tests-dialog/reject-ref-tests-dialog';
import { SendInvitationsDialog } from '../list/components/dialogs/send-invitations-dialog/send-invitations-dialog';
import { SendResultsDialog } from '../list/components/dialogs/send-results-dialog/send-results-dialog';
import { RefTestDetailData } from './services/ref-test-detail-data';
import { RefTestDetailOperationManager } from './services/ref-test-detail-operation-manager';

@Component({
  selector: 'app-ref-test-detail',
  imports: [
    TranslatePipe,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    SendInvitationsDialog,
    SendResultsDialog,
    DeleteRefTestsDialog,
    ApproveRefTestsDialog,
    RejectRefTestsDialog,
    Banner,
    HasPermission,
  ],
  providers: [RefTestDetailOperationManager],
  templateUrl: './ref-test-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetail {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _dataService = inject(RefTestDetailData);
  protected readonly operationManager = inject(RefTestDetailOperationManager);

  protected readonly refTestData = this._dataService.refTestData;
  protected readonly loading = this._dataService.loading;
  protected readonly Permissions = Permissions;

  constructor() {
    // Subscribe to ref test updates for the current ref test
    this._route.paramMap
      .pipe(
        map((params) => params.get('id')),
        filter((id): id is string => !!id),
        tap((currentId) => this._dataService.setRefTestId(currentId)),
        catchError(() => {
          return EMPTY;
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();

    this._dataService.subscribeToRefTestUpdates(this._destroyRef).subscribe();
  }

  // Computed properties for actions
  protected readonly canSendInvitation = computed(() => {
    return this.refTestData().status === 'PENDING';
  });

  protected readonly canSendResults = computed(() => {
    return this.refTestData().status === 'COMPLETED';
  });

  protected readonly invitationSummary = computed(() => {
    const refTest = this.refTestData();
    const participant = { name: refTest.name, email: refTest.email };
    return {
      newInvitations: refTest.invitationSent ? [] : [participant],
      resendInvitations: refTest.invitationSent ? [participant] : [],
    };
  });

  protected readonly resultsSummary = computed(() => {
    const refTest = this.refTestData();
    const participant = { name: refTest.name, email: refTest.email };
    return {
      newResults: refTest.resultsSent ? [] : [participant],
      resendResults: refTest.resultsSent ? [participant] : [],
    };
  });

  protected readonly refTestsToDelete = computed(() => {
    const refTest = this.refTestData();
    return [{ name: refTest.name, email: refTest.email }];
  });

  protected readonly canApprove = computed(() => {
    const s = this.refTestData().status;
    return s === 'PENDING_APPROVAL' || s === 'REJECTED';
  });

  protected readonly canReject = computed(() => this.refTestData().status === 'PENDING_APPROVAL');

  protected readonly refTestForApproval = computed(() => {
    const refTest = this.refTestData();
    return [{ name: refTest.name, email: refTest.email }];
  });

  protected getStatusClass(status: RefTestStatus): string {
    switch (status) {
      case 'COMPLETED':
        return 'bg-success-100 text-success-800';
      case 'IN_PROGRESS':
        return 'bg-blue-100 text-blue-800';
      case 'EXPIRED':
        return 'bg-red-100 text-red-800';
      case 'PENDING_APPROVAL':
        return 'bg-orange-100 text-orange-800';
      case 'REJECTED':
        return 'bg-error-100 text-error-800';
      case 'PENDING':
      default:
        return 'bg-yellow-100 text-yellow-800';
    }
  }

  protected navigateBack(): void {
    this._router.navigate(['/ref-tests']);
  }

  // ========================================================================
  // DIALOG OPERATIONS
  // ========================================================================

  protected confirmSendInvitation(): void {
    this.operationManager.sendInvitationDialog.confirm();
  }

  protected confirmSendResults(): void {
    this.operationManager.sendResultDialog.confirm();
  }

  protected confirmDelete(): void {
    this.operationManager.deleteDialog.confirm();
  }

  protected confirmApprove(): void {
    this.operationManager.approveDialog.confirm();
  }

  protected confirmReject(reason: string): void {
    this.operationManager.rejectDialog.confirm(reason);
  }
}
