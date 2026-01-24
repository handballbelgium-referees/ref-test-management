import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRoute,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { onlyCompleteData } from 'apollo-angular';
import { catchError, EMPTY, finalize, map, switchMap, tap } from 'rxjs';
import {
  DeleteRefTestsGQL,
  GetRefTestByIdGQL,
  GetRefTestsAllCountsDocument,
  RefTestStatus,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
} from '../../../../graphql/generated';
import { Toast } from '../../services/toast';
import { DeleteRefTestsDialog } from '../list/components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog';
import { SendInvitationsDialog } from '../list/components/dialogs/send-invitations-dialog/send-invitations-dialog';
import { SendResultsDialog } from '../list/components/dialogs/send-results-dialog/send-results-dialog';
import { RefTestDetailDataService } from './services/ref-test-detail-data.service';

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
  ],
  providers: [RefTestDetailDataService],
  templateUrl: './ref-test-detail.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetail {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _getRefTestByIdGQL = inject(GetRefTestByIdGQL);
  private readonly _dataService = inject(RefTestDetailDataService);
  private readonly _sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly _sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly _deleteRefTestsGQL = inject(DeleteRefTestsGQL);
  private readonly _toastService = inject(Toast);
  private readonly _translateService = inject(TranslateService);

  protected readonly RefTestStatus = RefTestStatus;

  // Dialog state
  protected readonly showSendInvitationDialog = signal(false);
  protected readonly showSendResultsDialog = signal(false);
  protected readonly showDeleteDialog = signal(false);

  // Operation state
  protected readonly sendingInvitation = signal(false);
  protected readonly sendingResults = signal(false);
  protected readonly deletingRefTest = signal(false);

  protected readonly refTestData = toSignal(
    this._route.paramMap.pipe(
      switchMap((params) => {
        const id = params.get('id');
        if (!id) {
          this._router.navigate(['/ref-tests']);
          return EMPTY;
        }

        return this._getRefTestByIdGQL.watch({ variables: { id } }).valueChanges.pipe(
          onlyCompleteData(),
          map((result) => {
            if (!result.data.refTest) {
              this._router.navigate(['/ref-tests']);
              return null;
            }
            return result.data.refTest;
          }),
          catchError(() => {
            this._router.navigate(['/ref-tests']);
            return EMPTY;
          }),
        );
      }),
      takeUntilDestroyed(this._destroyRef),
    ),
  );

  constructor() {
    // Update the data service whenever refTestData changes
    effect(() => {
      const data = this.refTestData();
      if (data) {
        this._dataService.setRefTest(data);
      }
    });
  }

  protected readonly loading = computed(() => !this.refTestData());

  // Computed properties for actions
  protected readonly canSendInvitation = computed(() => {
    const data = this.refTestData();
    return data?.status === RefTestStatus.Pending;
  });

  protected readonly canSendResults = computed(() => {
    const data = this.refTestData();
    return data?.status === RefTestStatus.Completed;
  });

  protected readonly invitationSummary = computed(() => {
    const data = this.refTestData();
    if (!data) return { newInvitations: [], resendInvitations: [] };

    const participant = { name: data.name || '', email: data.email || '' };
    return {
      newInvitations: data.invitationSent ? [] : [participant],
      resendInvitations: data.invitationSent ? [participant] : [],
    };
  });

  protected readonly resultsSummary = computed(() => {
    const data = this.refTestData();
    if (!data) return { newResults: [], resendResults: [] };

    const participant = { name: data.name || '', email: data.email || '' };
    return {
      newResults: data.resultsSent ? [] : [participant],
      resendResults: data.resultsSent ? [participant] : [],
    };
  });

  protected readonly refTestsToDelete = computed(() => {
    const data = this.refTestData();
    if (!data) return [];

    return [{ name: data.name || '', email: data.email || '' }];
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

  protected openSendInvitationDialog(): void {
    this.showSendInvitationDialog.set(true);
  }

  protected confirmSendInvitation(): void {
    this.showSendInvitationDialog.set(false);
    const data = this.refTestData();
    if (!data) return;

    this.sendingInvitation.set(true);
    this._sendInvitationsGQL
      .mutate({ variables: { input: { ids: [data.id] } } })
      .pipe(
        tap((result) => {
          if (result.data?.sendInvitations) {
            this._toastService.success(
              this._translateService.instant('ref_tests.detail.invitation_sent'),
            );
          }
        }),
        catchError(() => {
          this._toastService.error(
            this._translateService.instant('ref_tests.detail.invitation_error'),
          );
          return EMPTY;
        }),
        finalize(() => this.sendingInvitation.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected cancelSendInvitation(): void {
    this.showSendInvitationDialog.set(false);
  }

  // ========================================================================
  // RESULTS OPERATIONS
  // ========================================================================

  protected openSendResultsDialog(): void {
    this.showSendResultsDialog.set(true);
  }

  protected confirmSendResults(): void {
    this.showSendResultsDialog.set(false);
    const data = this.refTestData();
    if (!data) return;

    this.sendingResults.set(true);
    this._sendResultsGQL
      .mutate({ variables: { input: { ids: [data.id] } } })
      .pipe(
        tap((result) => {
          if (result.data?.sendResults) {
            this._toastService.success(
              this._translateService.instant('ref_tests.detail.results_sent'),
            );
          }
        }),
        catchError(() => {
          this._toastService.error(
            this._translateService.instant('ref_tests.detail.results_error'),
          );
          return EMPTY;
        }),
        finalize(() => this.sendingResults.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected cancelSendResults(): void {
    this.showSendResultsDialog.set(false);
  }

  // ========================================================================
  // DELETE OPERATIONS
  // ========================================================================

  protected openDeleteDialog(): void {
    this.showDeleteDialog.set(true);
  }

  protected confirmDelete(): void {
    this.showDeleteDialog.set(false);
    const data = this.refTestData();
    if (!data) return;

    this.deletingRefTest.set(true);
    this._deleteRefTestsGQL
      .mutate({
        variables: { input: { ids: [data.id] } },
        refetchQueries: [{ query: GetRefTestsAllCountsDocument }],
      })
      .pipe(
        tap((result) => {
          if (result.data?.deleteRefTests) {
            this._toastService.success(
              this._translateService.instant('ref_tests.detail.delete_success'),
            );
            // Navigate back to list after successful delete
            this._router.navigate(['/ref-tests']);
          }
        }),
        catchError(() => {
          this._toastService.error(this._translateService.instant('ref_tests.detail.delete_error'));
          return EMPTY;
        }),
        finalize(() => this.deletingRefTest.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected cancelDelete(): void {
    this.showDeleteDialog.set(false);
  }
}
