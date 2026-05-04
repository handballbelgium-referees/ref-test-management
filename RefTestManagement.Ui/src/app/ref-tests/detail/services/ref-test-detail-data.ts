import { computed, DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { Apollo, QueryRef } from 'apollo-angular';
import { catchError, EMPTY, Observable, switchMap, tap } from 'rxjs';
import {
  ExtendRefTestTimeGQL,
  ExtendRefTestTimeInput,
  GetRefTestByIdGQL,
  GetRefTestByIdQuery,
  GetRefTestByIdQueryVariables,
  RefTestResetType,
  RefTestUpdatedGQL,
  RefTestUpdatedSubscription,
  RegenerateRefTestTokenGQL,
  RegenerateRefTestTokenInput,
  ResetRefTestsGQL,
  ReviveRefTestsGQL,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
  UpdateRefTestConfigurationGQL,
  UpdateRefTestConfigurationInput,
  UpdateRefTestDetailsGQL,
  UpdateRefTestDetailsInput,
  UpdateRefTestNotificationSettingsGQL,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../graphql/generated';
import { MutationCallbacks, runMutation } from '../../../shared/utils/apollo-utils';

type RefTest = Extract<GetRefTestByIdQuery['refTest'], { __typename: 'RefTest' }>;

@Injectable({ providedIn: 'root' })
export class RefTestDetailData {
  private readonly _getRefTestByIdGQL = inject(GetRefTestByIdGQL);
  private readonly _sendInvitationsGQL = inject(SendRefTestInvitationsGQL);
  private readonly _sendResultsGQL = inject(SendRefTestResultsGQL);
  private readonly _resetRefTestsGQL = inject(ResetRefTestsGQL);
  private readonly _reviveRefTestsGQL = inject(ReviveRefTestsGQL);
  private readonly _refTestUpdatedGQL = inject(RefTestUpdatedGQL);
  private readonly _updateRefTestDetailsGQL = inject(UpdateRefTestDetailsGQL);
  private readonly _updateRefTestConfigurationGQL = inject(UpdateRefTestConfigurationGQL);
  private readonly _updateRefTestNotificationSettingsGQL = inject(
    UpdateRefTestNotificationSettingsGQL,
  );
  private readonly _extendRefTestTimeGQL = inject(ExtendRefTestTimeGQL);
  private readonly _regenerateRefTestTokenGQL = inject(RegenerateRefTestTokenGQL);

  private readonly _router = inject(Router);

  private readonly _refTestId = signal<string>('');
  readonly refTestId = this._refTestId.asReadonly();

  /* ------------------------------------------------------------------------ */
  /* Query                                                                 */
  /* ------------------------------------------------------------------------ */

  private _queryRef?: QueryRef<GetRefTestByIdQuery, GetRefTestByIdQueryVariables>;

  private readonly _queryResult = toSignal(
    toObservable(this._refTestId).pipe(
      switchMap((id) => {
        if (!id) return EMPTY;

        const ref = this._getRefTestByIdGQL.watch({ variables: { id } });
        this._queryRef = ref;
        return ref.valueChanges;
      }),
      tap((result) => {
        if (result.data?.refTest?.__typename === 'RefTestNotFoundError') {
          throw new Error('Ref Test not found');
        }
      }),
      catchError(() => {
        this._router.navigate(['/ref-tests']);
        return EMPTY;
      }),
    ),
    { initialValue: null },
  );

  readonly loading = computed(() => this._queryResult()?.loading ?? false);
  readonly refTestData = computed(() => {
    const refTest = this._queryResult()?.data?.refTest;
    if (!this.loading() && refTest?.__typename !== 'RefTest') {
      throw new Error('Ref Test not found');
    }
    return refTest as RefTest;
  });

  /* ------------------------------------------------------------------------ */
  /* Setters                                                                 */
  /* ------------------------------------------------------------------------ */

  setRefTestId(id: string): void {
    this._refTestId.set(id);
  }

  /* ------------------------------------------------------------------------ */
  /* Mutations (Public API)                                                   */
  /* ------------------------------------------------------------------------ */

  sendInvitations(
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._sendInvitationsGQL.mutate({ variables: { input: { ids: [this._refTestId()] } } }),
      destroyRef,
      callbacks,
      (r) => r.data?.sendInvitations?.sendInvitationsResult?.sentRefTests.map((s) => s.id) ?? [],
    );
  }

  sendResults(
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<string[]> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._sendResultsGQL.mutate({ variables: { input: { ids: [this._refTestId()] } } }),
      destroyRef,
      callbacks,
      (r) => r.data?.sendResults?.sendResultsResult?.sentRefTests.map((s) => s.id) ?? [],
    );
  }

  resetRefTests(
    input: {
      resetType: RefTestResetType;
      regenerateToken: boolean;
    },
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._resetRefTestsGQL.mutate({
        variables: {
          input: {
            ids: [this._refTestId()],
            resetType: input.resetType,
            regenerateToken: input.regenerateToken,
          },
        },
      }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.resetRefTests?.resetRefTestsResult;
        return {
          successCount: res?.successfullyReset ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  reviveRefTests(
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<{ successCount: number; failedCount: number }> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._reviveRefTestsGQL.mutate({ variables: { input: { ids: [this._refTestId()] } } }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.reviveRefTests?.reviveRefTestsResult;
        return {
          successCount: res?.successfullyRevived ?? 0,
          failedCount: res?.failed ?? 0,
        };
      },
    );
  }

  editParticipantDetails(
    input: UpdateRefTestDetailsInput,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<void> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._updateRefTestDetailsGQL.mutate({
        variables: {
          input: {
            id: this._refTestId(),
            firstName: input.firstName,
            lastName: input.lastName,
            email: input.email,
          },
        },
      }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.updateRefTestDetails.refTest;
        return res;
      },
    );
  }

  updateRefTestConfiguration(
    input: UpdateRefTestConfigurationInput,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<void> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._updateRefTestConfigurationGQL.mutate({ variables: { input } }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.updateRefTestConfiguration.refTest;
        return res;
      },
    );
  }

  updateRefTestNotificationSettings(
    input: UpdateRefTestNotificationSettingsInput,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<void> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._updateRefTestNotificationSettingsGQL.mutate({ variables: { input } }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.updateRefTestNotificationSettings.refTest;
        return res;
      },
    );
  }

  extendRefTestTime(
    input: ExtendRefTestTimeInput,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<void> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._extendRefTestTimeGQL.mutate({ variables: { input } }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.extendRefTestTime.refTest;
        return res;
      },
    );
  }

  regenerateRefTestToken(
    input: RegenerateRefTestTokenInput,
    destroyRef: DestroyRef,
    callbacks: MutationCallbacks<void> = {},
  ): { loading: Signal<boolean>; success: Signal<boolean> } {
    return runMutation(
      this._regenerateRefTestTokenGQL.mutate({ variables: { input } }),
      destroyRef,
      callbacks,
      (r) => {
        const res = r.data?.regenerateRefTestToken.refTest;
        return res;
      },
    );
  }

  /* ------------------------------------------------------------------------ */
  /* Subscription + Cache Updates                                             */
  /* ------------------------------------------------------------------------ */

  public subscribeToRefTestUpdates(
    destroyRef: DestroyRef,
  ): Observable<Apollo.SubscribeResult<RefTestUpdatedSubscription>> {
    return toObservable(this._refTestId).pipe(
      switchMap((id) =>
        this._refTestUpdatedGQL.subscribe({ variables: { id } }).pipe(
          tap((result) => {
            const event = result.data?.refTestUpdated;
            if (!event) return;

            const current = this._queryRef?.getCurrentResult();
            if (current?.data?.refTest?.__typename !== 'RefTest') return;
            const isLoaded = current?.data?.refTest?.id === event.id;

            if (!isLoaded) return;
            // Map subscription event to cache update
            let updates: Record<string, unknown> = {};
            switch (event.__typename) {
              case 'RefTestCompleted':
                updates = {
                  status: event.status,
                  completedAt: event.completedAt,
                  questionScore: event.questionScore,
                  questionTotal: event.questionTotal,
                  answerScore: event.answerScore,
                  answerTotal: event.answerTotal,
                  percentage: event.percentage,
                  language: event.language,
                };
                break;

              case 'RefTestExpired':
                updates = { status: event.status };
                break;

              case 'RefTestStarted':
                updates = { status: event.status, startedAt: event.startedAt };
                break;

              case 'RefTestInvitationSent':
                updates = { invitationSent: true };
                break;

              case 'RefTestResultSent':
                updates = { resultsSent: true };
                break;
            }

            this.updateRefTestData(updates);
          }),
          catchError(() => EMPTY),
          takeUntilDestroyed(destroyRef),
        ),
      ),
    );
  }

  private updateRefTestData(event: Record<string, unknown>): void {
    if (!this._queryRef) return;

    // Extract only the properties that should be merged, excluding __typename
    const { __typename, ...updates } = event;

    // Update Apollo cache
    // @ts-expect-error - Apollo's updateQuery has complex typing that doesn't match our return
    this._queryRef.updateQuery((prev) => {
      if (!prev?.refTest) return prev;

      return {
        ...prev,
        refTest: {
          ...prev.refTest,
          ...updates,
        },
      };
    });
  }
}
