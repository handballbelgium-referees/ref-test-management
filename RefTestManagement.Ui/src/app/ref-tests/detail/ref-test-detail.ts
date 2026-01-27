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
import { Banner } from '../../shared/components/banner/banner';
import { DeleteRefTestsDialog } from '../list/components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog';
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
    Banner,
  ],
  templateUrl: './ref-test-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetail {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _dataService = inject(RefTestDetailData);
  protected readonly operationManager = inject(RefTestDetailOperationManager);

  protected readonly RefTestStatus = RefTestStatus;
  protected readonly refTestData = this._dataService.refTestData;
  protected readonly loading = this._dataService.loading;

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
    return this.refTestData().status === RefTestStatus.Pending;
  });

  protected readonly canSendResults = computed(() => {
    return this.refTestData().status === RefTestStatus.Completed;
  });

  protected readonly invitationSummary = computed(() => {
    if (!this.refTestData()) return { newInvitations: [], resendInvitations: [] };

    const participant = {
      name: this.refTestData().name,
      email: this.refTestData().email,
    };
    return {
      newInvitations: this.refTestData().invitationSent ? [] : [participant],
      resendInvitations: this.refTestData().invitationSent ? [participant] : [],
    };
  });

  protected readonly resultsSummary = computed(() => {
    if (!this.refTestData()) return { newResults: [], resendResults: [] };

    const participant = {
      name: this.refTestData().name,
      email: this.refTestData().email,
    };
    return {
      newResults: this.refTestData().resultsSent ? [] : [participant],
      resendResults: this.refTestData().resultsSent ? [participant] : [],
    };
  });

  protected readonly refTestsToDelete = computed(() => {
    if (!this.refTestData()) return [];

    return [{ name: this.refTestData()?.name, email: this.refTestData()?.email }];
  });

  protected getStatusClass(status: RefTestStatus): string {
    switch (status) {
      case RefTestStatus.Completed:
        return 'bg-success-100 text-success-800';
      case RefTestStatus.InProgress:
        return 'bg-blue-100 text-blue-800';
      case RefTestStatus.Expired:
        return 'bg-red-100 text-red-800';
      case RefTestStatus.Pending:
      default:
        return 'bg-yellow-100 text-yellow-800';
    }
  }

  protected navigateBack(): void {
    this._router.navigate(['/ref-tests']);
  }

  // ========================================================================
  // INVITATION OPERATIONS
  // ========================================================================

  protected confirmSendInvitation(): void {
    this.operationManager.sendInvitationDialog.confirm(this._destroyRef);
  }

  // ========================================================================
  // RESULTS OPERATIONS
  // ========================================================================
  protected confirmSendResults(): void {
    this.operationManager.sendResultDialog.confirm(this._destroyRef);
  }

  // ========================================================================
  // DELETE OPERATIONS
  // ========================================================================

  protected confirmDelete(): void {
    this.operationManager.deleteDialog.confirm(this._destroyRef);
  }
}
