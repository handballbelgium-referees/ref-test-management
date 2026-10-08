import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, EMPTY, map, tap } from 'rxjs';
import {
  AcknowledgeFailedPrivacyWithdrawalTargetGQL,
  GetFailedPrivacyWithdrawalTargetsGQL,
} from '../../../../../graphql/generated';
import { runMutation } from '../../../shared/utils/apollo-utils';

const PAGE_SIZE = 20;

interface FailedPrivacyWithdrawalTarget {
  readonly actionToken: string;
  readonly failureCategory: string;
}

@Component({
  selector: 'app-privacy-withdrawal-review',
  imports: [TranslatePipe],
  templateUrl: './privacy-withdrawal-review.html',
  host: {
    class: 'block',
  },
})
export class PrivacyWithdrawalReview {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _getFailedTargetsGQL = inject(GetFailedPrivacyWithdrawalTargetsGQL);
  private readonly _acknowledgeFailedTargetGQL = inject(
    AcknowledgeFailedPrivacyWithdrawalTargetGQL,
  );
  private readonly _queryRef = this._getFailedTargetsGQL.watch({
    variables: { first: PAGE_SIZE },
    fetchPolicy: 'no-cache',
  });
  private _pageFetchGeneration = 0;

  readonly loading = signal(true);
  readonly queryError = signal(false);
  readonly hasNextPage = signal(false);
  readonly endCursor = signal<string | null>(null);
  readonly loadingMore = signal(false);
  readonly loadMoreError = signal(false);
  readonly acknowledging = signal(false);
  readonly acknowledgementError = signal(false);
  readonly acknowledgementStatus = signal<'acknowledged' | 'notAvailable' | null>(null);
  private readonly _dismissedActionToken = signal<string | null>(null);
  private readonly _queryTargets = toSignal(
    this._queryRef.valueChanges.pipe(
      tap((result) => {
        const connection = result.data?.failedPrivacyWithdrawalTargets;
        if (connection) {
          this.hasNextPage.set(connection.pageInfo?.hasNextPage ?? false);
          this.endCursor.set(connection.pageInfo?.endCursor ?? null);
        }
        this.loading.set(result.loading);
        this.queryError.set(!!result.error && !connection);
      }),
      map((result) => this._readTargets(result.data?.failedPrivacyWithdrawalTargets?.edges)),
      catchError(() => {
        this.loading.set(false);
        this.queryError.set(true);
        return EMPTY;
      }),
    ),
    { initialValue: [] },
  );
  readonly targets = computed(() => {
    const dismissedActionToken = this._dismissedActionToken();
    return this._queryTargets().filter((target) => target.actionToken !== dismissedActionToken);
  });

  protected retry(): void {
    void this._refreshFirstPage();
  }

  protected loadMore(): void {
    const after = this.endCursor();
    if (this.acknowledging() || !this.hasNextPage() || !after || this.loadingMore()) return;

    const generation = ++this._pageFetchGeneration;
    this.loadMoreError.set(false);
    this.loadingMore.set(true);
    void this._queryRef
      .fetchMore({
        variables: { first: PAGE_SIZE, after },
        updateQuery: (previous, { fetchMoreResult }) => {
          if (generation !== this._pageFetchGeneration) return previous;

          const previousConnection = previous.failedPrivacyWithdrawalTargets;
          const nextConnection = fetchMoreResult?.failedPrivacyWithdrawalTargets;
          if (!previousConnection || !nextConnection) return previous;

          return {
            ...previous,
            failedPrivacyWithdrawalTargets: {
              ...previousConnection,
              edges: [...(previousConnection.edges ?? []), ...(nextConnection.edges ?? [])],
              pageInfo: nextConnection.pageInfo,
            },
          };
        },
      })
      .then(() => {
        if (generation === this._pageFetchGeneration) this.loadingMore.set(false);
      })
      .catch(() => {
        if (generation !== this._pageFetchGeneration) return;

        this.loadingMore.set(false);
        this.loadMoreError.set(true);
      });
  }

  protected acknowledge(actionToken: string): void {
    if (this.acknowledging()) return;

    this.acknowledgementError.set(false);
    this.acknowledgementStatus.set(null);
    let refreshPending = false;
    runMutation(
      this._acknowledgeFailedTargetGQL.mutate({ variables: { input: { actionToken } } }),
      this._destroyRef,
      {
        onStart: () => {
          this._pageFetchGeneration++;
          this.loadingMore.set(false);
          this.loadMoreError.set(false);
          this.acknowledging.set(true);
        },
        onSuccess: (status) => {
          if (status !== 'ACKNOWLEDGED' && status !== 'NOT_AVAILABLE') {
            this.acknowledgementError.set(true);
            return;
          }

          this._dismissedActionToken.set(actionToken);
          this.acknowledgementStatus.set(
            status === 'ACKNOWLEDGED' ? 'acknowledged' : 'notAvailable',
          );
          refreshPending = true;
          void this._refreshFirstPage().finally(() => this.acknowledging.set(false));
        },
        onError: () => this.acknowledgementError.set(true),
        onComplete: () => {
          if (!refreshPending) this.acknowledging.set(false);
        },
      },
      (result) =>
        result.data?.acknowledgeFailedPrivacyWithdrawalTarget
          ?.privacyWithdrawalTargetAcknowledgementResult?.status ?? 'NOT_AVAILABLE',
    );
  }

  private async _refreshFirstPage(): Promise<void> {
    this.queryError.set(false);
    this.loading.set(true);
    this.hasNextPage.set(false);
    this.endCursor.set(null);
    this.loadMoreError.set(false);
    try {
      await this._queryRef.refetch({ first: PAGE_SIZE, after: null });
      this._dismissedActionToken.set(null);
    } catch {
      this.loading.set(false);
      this.queryError.set(true);
    }
  }

  private _readTargets(
    edges:
      | ReadonlyArray<{
          readonly node?: {
            readonly actionToken?: string;
            readonly failureCategory?: string;
          } | null;
        } | null>
      | null
      | undefined,
  ): FailedPrivacyWithdrawalTarget[] {
    return (
      edges
        ?.map((edge) => edge?.node)
        .filter(
          (
            target,
          ): target is {
            readonly actionToken: string;
            readonly failureCategory: string;
          } => typeof target?.actionToken === 'string'
            && typeof target.failureCategory === 'string',
        )
        .map((target) => ({
          actionToken: target.actionToken,
          failureCategory: target.failureCategory,
        })) ?? []
    );
  }
}
