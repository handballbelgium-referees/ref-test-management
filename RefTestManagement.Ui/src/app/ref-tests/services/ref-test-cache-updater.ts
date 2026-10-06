import { inject, Service } from '@angular/core';
import { ApolloCache, ApolloLink } from '@apollo/client';
import { Apollo } from 'apollo-angular';
import {
  DeleteRefTestsMutation,
  RefTestStatus,
} from '../../../../graphql/generated';
import { MutationCallbacks } from '../../shared/utils/apollo-utils';
import { RefTestFilterState } from '../list/services/ref-test-filter-state';
import { defer, from, Observable, Subject, tap } from 'rxjs';
import {
  getRefTestCacheStatus,
  planRefTestCacheUpdate,
  RefTestCacheSnapshot,
} from './ref-test-cache-planner';
import { RefTestCacheWriter } from './ref-test-cache-writer';

type RefTestSubscriptionEvent = {
  readonly __typename: string;
  readonly id: string;
  readonly status?: RefTestStatus;
  readonly [field: string]: unknown;
};

type RefTestSubscriptionSource = 'list' | 'detail';

type PendingRefTestSubscriptionEvent = {
  readonly event: RefTestSubscriptionEvent;
  readonly detailBefore?: unknown;
  readonly source: RefTestSubscriptionSource | 'mutation';
  readonly cacheBaselineUncertain: boolean;
};

export type RefTestCacheRefresh = {
  readonly lists: boolean;
  readonly counts: boolean;
};

type RefTestTransitionEventType =
  | 'RefTestApproved'
  | 'RefTestRejected'
  | 'RefTestReset'
  | 'RefTestRevived';

type PendingRefTestTransition = {
  readonly eventType: RefTestTransitionEventType;
  readonly before: RefTestCacheSnapshot;
};

type RefTestTransitionRow = { readonly id: string; readonly createdAt?: unknown };

/** Tracks one local mutation whose subscription event may still be pending. */
export type RefTestTransition = {
  /**
   * Mutation `update` hook, run after the response is written. Rows the server did not
   * transition are forgotten, and a returned CreatedAt supersedes any stale marker.
   */
  readonly confirm: (rows: readonly RefTestTransitionRow[] | null | undefined) => void;
  /** Wraps mutation callbacks so a failed mutation forgets its pre-mutation snapshots. */
  readonly cancelOnError: <T>(callbacks: MutationCallbacks<T>) => MutationCallbacks<T>;
};

/**
 * Events whose transition replaces CreatedAt without reporting the new value: approval and
 * revival always do, and a reset event does not reveal whether it was a (replacing) hard reset.
 */
const CREATED_AT_REPLACING_EVENTS: ReadonlySet<string> = new Set([
  'RefTestApproved',
  'RefTestRevived',
  'RefTestReset',
]);

/** Applies RefTest subscription and deletion events to the production Apollo cache policy. */
@Service()
export class RefTestCacheUpdater {
  private readonly _apollo = inject(Apollo);
  private readonly _cacheWriter = inject(RefTestCacheWriter);
  private readonly _filterState = inject(RefTestFilterState);
  /** Local deletes awaiting their mutation response, which is their sole cache updater. */
  private readonly _pendingDeleteIds = new Set<string>();
  private readonly _processedSubscriptionDeleteIds = new Set<string>();
  private readonly _subscriptionStatuses = new Map<string, RefTestStatus>();
  private readonly _pendingSubscriptionEvents: PendingRefTestSubscriptionEvent[] = [];
  private readonly _refreshRequests = new Subject<RefTestCacheRefresh>();
  private readonly _detailQueriesLoading = new Set<string>();
  private _listQueryLoading = false;
  private _countsQueryLoading = false;
  private _listReadsInFlight = 0;
  private _countsReadsInFlight = 0;
  private _replayingPendingEvents = false;
  private _cacheInvalidatedDuringReplay = false;
  private _deferredRefresh: RefTestCacheRefresh | null = null;
  /** Pre-mutation state of local transitions whose event has not been applied yet. */
  private readonly _pendingTransitions = new Map<string, PendingRefTestTransition>();
  /**
   * Cached CreatedAt values (by id) the server has since replaced. The cache keeps them because
   * removing a selected field would trigger a refetch; ordering ignores a value only while the
   * cache still holds it, so any different value from a query or mutation response restores it.
   */
  private readonly _staleCreatedAt = new Map<string, unknown>();

  public readonly refreshRequests = this._refreshRequests.asObservable();

  public markPendingDeletes(ids: readonly string[]): void {
    for (const id of ids) this._pendingDeleteIds.add(id);
  }

  public clearPendingDeletes(ids: readonly string[]): void {
    for (const id of ids) this._pendingDeleteIds.delete(id);
  }

  /**
   * Snapshots rows before a local approve, reject, reset or revive mutation. The mutation
   * response can normalize the new status before the matching event arrives, which would hide
   * the transition from the counters and list edges. The first matching event applied for a row
   * consumes its snapshot as the pre-transition state, so whichever arrives first yields exactly
   * one transition and a duplicate delivery falls back to the cache. Completion deliberately
   * does not clear the snapshots: it also fires when the caller is destroyed before the response
   * arrives, and the event may legitimately follow the response.
   */
  public beginTransition(
    eventType: RefTestTransitionEventType,
    ids: readonly string[],
  ): RefTestTransition {
    const cache = this._apollo.client.cache;
    const started = new Map<string, PendingRefTestTransition>();
    for (const id of ids) {
      const { snapshot } = this._cacheWriter.readSnapshot(cache, id);
      if (!snapshot) continue;

      const transition = { eventType, before: snapshot };
      started.set(id, transition);
      this._pendingTransitions.set(id, transition);
    }

    const forget = (id: string) => {
      if (this._pendingTransitions.get(id) === started.get(id)) {
        this._pendingTransitions.delete(id);
      }
    };

    return {
      confirm: (rows) => {
        const confirmed = new Set<string>();
        for (const row of rows ?? []) {
          confirmed.add(row.id);
          if (row.createdAt !== undefined) this._staleCreatedAt.delete(row.id);
        }
        for (const id of started.keys()) {
          if (!confirmed.has(id)) forget(id);
        }
      },
      cancelOnError: (callbacks) => ({
        ...callbacks,
        onError: (error) => {
          for (const id of started.keys()) forget(id);
          callbacks.onError?.(error);
        },
      }),
    };
  }

  public updateDeleteCache(
    cache: ApolloCache,
    { data }: ApolloLink.Result<DeleteRefTestsMutation>,
  ): void {
    const deleted = data?.deleteRefTests?.deleteRefTestsResult?.deletedRefTests ?? [];

    for (const item of deleted) {
      this._pendingDeleteIds.delete(item.id);
      this.applySubscriptionEvent(
        cache,
        { __typename: 'RefTestDeleted', id: item.id, status: item.status },
        undefined,
        'mutation',
      );
    }
  }

  public updateCacheFromSubscription(
    event: RefTestSubscriptionEvent,
    detailBefore?: unknown,
    source: RefTestSubscriptionSource = 'detail',
  ): boolean {
    return this.applySubscriptionEvent(this._apollo.client.cache, event, detailBefore, source);
  }

  public buildCountsVariables() {
    return this._cacheWriter.buildCountsVariables();
  }

  public setListQueryLoading(loading: boolean): void {
    this._listQueryLoading = loading;
  }

  public setCountsQueryLoading(loading: boolean): void {
    this._countsQueryLoading = loading;
  }

  public setDetailQueryLoading(id: string, loading: boolean): void {
    if (loading) this._detailQueriesLoading.add(id);
    else this._detailQueriesLoading.delete(id);
  }

  public trackListRead<T>(read: () => Promise<T>): Observable<T> {
    return this.trackRead(
      read,
      () => this._listReadsInFlight++,
      () => this._listReadsInFlight--,
    );
  }

  public trackCountsRead<T>(read: () => Promise<T>): Observable<T> {
    return this.trackRead(
      read,
      () => this._countsReadsInFlight++,
      () => this._countsReadsInFlight--,
    );
  }

  /** Replays events received before query data was available or while a read was in flight. */
  public replayPendingEvents(): void {
    if (this.hasListOrCountsReadInFlight()) return;
    const pending = this._pendingSubscriptionEvents.splice(0);
    const cache = this._apollo.client.cache;
    this._replayingPendingEvents = true;
    this._cacheInvalidatedDuringReplay = false;
    try {
      for (let index = 0; index < pending.length; index++) {
        const queued = pending[index];
        if (
          this._detailQueriesLoading.has(queued.event.id) ||
          (!this._cacheInvalidatedDuringReplay && !this.hasRequiredCache(cache, queued))
        ) {
          this._pendingSubscriptionEvents.push(...pending.slice(index));
          break;
        }
        this.applySubscriptionEvent(
          cache,
          queued.event,
          queued.detailBefore,
          queued.source,
          true,
          queued.cacheBaselineUncertain,
        );
      }
    } finally {
      this._replayingPendingEvents = false;
      this._cacheInvalidatedDuringReplay = false;
      if (this._deferredRefresh) {
        const refresh = this._deferredRefresh;
        this._deferredRefresh = null;
        this._refreshRequests.next(refresh);
      }
    }
  }

  private trackRead<T>(
    read: () => Promise<T>,
    start: () => void,
    finish: () => void,
  ): Observable<T> {
    return new Observable<T>((subscriber) => {
      start();
      let finished = false;
      const finishRead = () => {
        if (finished) return;
        finished = true;
        finish();
        this.replayPendingEvents();
      };

      // Apollo's Promise requests continue after unsubscribe; keep tracking until they settle.
      defer(() => from(read()))
        .pipe(
          tap({
            next: finishRead,
            error: finishRead,
            complete: finishRead,
          }),
        )
        .subscribe({
          next: (value) => subscriber.next(value),
          error: (error) => subscriber.error(error),
          complete: () => subscriber.complete(),
        });
    });
  }

  private hasListOrCountsReadInFlight(): boolean {
    return (
      this._listQueryLoading ||
      this._countsQueryLoading ||
      this._listReadsInFlight > 0 ||
      this._countsReadsInFlight > 0
    );
  }

  private hasReadInFlight(id: string): boolean {
    return this.hasListOrCountsReadInFlight() || this._detailQueriesLoading.has(id);
  }

  private hasRequiredCache(
    cache: ApolloCache,
    queued: PendingRefTestSubscriptionEvent,
  ): boolean {
    if (queued.source === 'detail') {
      return Boolean(
        this._cacheWriter.readSnapshot(cache, queued.event.id).snapshot ||
          this.toSnapshot(queued.detailBefore, queued.event.id),
      );
    }

    const cachedLists = this._cacheWriter.readCachedLists(cache);
    const activeStatus = this._filterState.filter().status;
    return (
      Boolean(cachedLists.get(activeStatus)?.refTests) && this._cacheWriter.hasCachedCounts(cache)
    );
  }

  private requestRefresh(refresh: RefTestCacheRefresh): void {
    if (!refresh.lists && !refresh.counts) return;
    if (this._replayingPendingEvents) {
      this._deferredRefresh = {
        lists: Boolean(this._deferredRefresh?.lists || refresh.lists),
        counts: Boolean(this._deferredRefresh?.counts || refresh.counts),
      };
      return;
    }
    this._refreshRequests.next(refresh);
  }

  private applySubscriptionEvent(
    cache: ApolloCache,
    event: RefTestSubscriptionEvent,
    detailBefore?: unknown,
    source: RefTestSubscriptionSource | 'mutation' = 'detail',
    replayed = false,
    cacheBaselineUncertain = false,
  ): boolean {
    if (event.__typename === 'RefTestDeleted') {
      if (this._pendingDeleteIds.has(event.id) && source !== 'mutation') return true;
      if (this._processedSubscriptionDeleteIds.has(event.id)) return true;
    }

    const { entityId, snapshot: cachedBefore } = this._cacheWriter.readSnapshot(cache, event.id);
    const rememberedStatus = this._subscriptionStatuses.get(event.id);
    const before = this.mergeSnapshots(
      this.mergeSnapshots(
        rememberedStatus === undefined
          ? null
          : { __typename: 'RefTest', id: event.id, status: rememberedStatus },
        this.toSnapshot(detailBefore, event.id),
        event.id,
      ),
      cachedBefore,
      event.id,
    );
    const eventFields = event as unknown as Record<string, unknown>;
    const isDeleted = event.__typename === 'RefTestDeleted';
    const patch = this.getSubscriptionPatch(event.__typename, eventFields);

    if (patch === null) return false;

    const cachedLists = this._cacheWriter.readCachedLists(cache);
    const hasDetailBefore = this.toSnapshot(detailBefore, event.id) !== null;
    const readInFlight = this.hasReadInFlight(event.id);
    const queued = {
      event,
      detailBefore,
      source,
      cacheBaselineUncertain: cacheBaselineUncertain || readInFlight,
    };
    const cacheInvalidatedDuringReplay =
      replayed && this._replayingPendingEvents && this._cacheInvalidatedDuringReplay;
    if (
      readInFlight ||
      (!cacheInvalidatedDuringReplay && !this.hasRequiredCache(cache, queued))
    ) {
      this._pendingSubscriptionEvents.push(queued);
      return true;
    }

    let current = before;
    if (current?.['status'] === undefined) {
      const previousStatus = this.getPreviousStatus(event.__typename, eventFields);
      if (previousStatus !== undefined) {
        current = this.mergeSnapshots(current, { status: previousStatus }, event.id);
      }
    }

    // A cached status that already equals the event's means a mutation response or an earlier
    // delivery of this event got here first, so the cache can no longer show the transition.
    const advanced = patch['status'] !== undefined && before?.['status'] === patch['status'];
    const transition = this.claimTransition(event.__typename, event.id);
    const previous =
      advanced && transition ? this.mergeSnapshots(current, transition.before, event.id) : current;

    if (
      !advanced &&
      CREATED_AT_REPLACING_EVENTS.has(event.__typename) &&
      patch['createdAt'] === undefined
    ) {
      const createdAt = current?.['createdAt'];
      if (createdAt !== undefined) this._staleCreatedAt.set(event.id, createdAt);
    }

    const beforeIsAbsent = event.__typename === 'RefTestCreated' && previous === null;
    const after = isDeleted
      ? null
      : ({
          ...(current ?? {}),
          ...patch,
          __typename: 'RefTest',
          id: event.id,
        } as RefTestCacheSnapshot);
    const planAfter =
      after &&
      event.__typename === 'RefTestReset' &&
      !advanced &&
      patch['invitationSent'] === undefined
        ? { ...after, invitationSent: undefined }
        : after;
    const planned = planRefTestCacheUpdate({
      cachedLists,
      id: event.id,
      before: previous,
      after: planAfter,
      patch,
      beforeIsAbsent,
      afterIsAbsent: isDeleted,
      isDeleted,
      filter: this._filterState.filter(),
      staleCreatedAt: this._staleCreatedAt,
    });
    // A detail query can normalize the event target before replay, hiding membership/order changes.
    const plan = queued.cacheBaselineUncertain
      ? { ...planned, invalidateLists: true, invalidateCounts: true }
      : planned;

    this._cacheWriter.applyPlan(cache, {
      id: event.id,
      entityId,
      after,
      patch,
      eventType: event.__typename,
      isDeleted,
      plan,
    });
    if (plan.invalidateLists || plan.invalidateCounts) {
      if (this._replayingPendingEvents) this._cacheInvalidatedDuringReplay = true;
      this.requestRefresh({
        lists: plan.invalidateLists,
        counts: plan.invalidateCounts,
      });
    }

    if (isDeleted) {
      this._subscriptionStatuses.delete(event.id);
      this._pendingTransitions.delete(event.id);
      this._staleCreatedAt.delete(event.id);
      this._processedSubscriptionDeleteIds.add(event.id);
    } else {
      const status = getRefTestCacheStatus(after?.['status']);
      if (status !== undefined) this._subscriptionStatuses.set(event.id, status);
    }
    return true;
  }

  /** Takes the pre-mutation snapshot of the matching local transition, if one is pending. */
  private claimTransition(
    eventType: string,
    id: string,
  ): PendingRefTestTransition | undefined {
    const transition = this._pendingTransitions.get(id);
    if (transition?.eventType !== eventType) return undefined;

    this._pendingTransitions.delete(id);
    return transition;
  }

  private getPreviousStatus(
    eventType: string,
    event: Record<string, unknown>,
  ): RefTestStatus | undefined {
    switch (eventType) {
      case 'RefTestStarted':
      case 'RefTestExpired':
        return 'PENDING';
      case 'RefTestCompleted':
        return 'IN_PROGRESS';
      case 'RefTestRevived':
        return 'EXPIRED';
      case 'RefTestRejected':
        return 'PENDING_APPROVAL';
      case 'RefTestAnonymized':
      case 'RefTestDeleted':
        return getRefTestCacheStatus(event['status']);
      case 'RefTestReset':
        return getRefTestCacheStatus(event['oldStatus']);
      case 'RefTestApproved':
        return getRefTestCacheStatus(event['oldStatus']);
      default:
        return undefined;
    }
  }

  private toSnapshot(value: unknown, id: string): RefTestCacheSnapshot | null {
    if (typeof value !== 'object' || value === null) return null;

    const snapshot = value as Record<string, unknown>;
    if (snapshot['id'] !== id || snapshot['__typename'] !== 'RefTest') return null;
    return snapshot as RefTestCacheSnapshot;
  }

  private mergeSnapshots(
    cached: RefTestCacheSnapshot | null,
    supplied: RefTestCacheSnapshot | Record<string, unknown> | null,
    id: string,
  ): RefTestCacheSnapshot | null {
    if (!cached && !supplied) return null;

    const merged: Record<string, unknown> = { ...(cached ?? {}) };
    for (const [field, value] of Object.entries(supplied ?? {})) {
      if (value !== undefined) merged[field] = value;
    }
    merged['__typename'] = 'RefTest';
    merged['id'] = id;
    return merged as RefTestCacheSnapshot;
  }

  private getSubscriptionPatch(
    eventType: string,
    event: Record<string, unknown>,
  ): Record<string, unknown> | null {
    const patch: Record<string, unknown> = {};
    const copy = (field: string, target = field) => {
      const value = event[field];
      if (value !== undefined) patch[target] = value;
    };

    switch (eventType) {
      case 'RefTestStarted':
        copy('status');
        copy('startedAt');
        break;
      case 'RefTestCompleted':
        copy('status');
        copy('completedAt');
        copy('questionScore');
        copy('questionTotal');
        copy('answerScore');
        copy('answerTotal');
        copy('percentage');
        copy('language');
        copy('selectedAnswerIds');
        break;
      case 'RefTestExpired':
        copy('status');
        break;
      case 'RefTestInvitationSent':
        patch['invitationSent'] = true;
        break;
      case 'RefTestResultSent':
        patch['resultsSent'] = true;
        break;
      case 'RefTestDeleted':
        break;
      case 'RefTestAnonymized': {
        copy('status');
        copy('name');
        copy('email');
        patch['isAnonymized'] = true;
        patch['rejectionReason'] = null;
        const name = event['name'];
        if (typeof name === 'string') {
          const separator = name.indexOf(' ');
          if (separator >= 0) {
            patch['firstName'] = name.slice(0, separator);
            patch['lastName'] = name.slice(separator + 1);
          }
        }
        break;
      }
      case 'RefTestReset':
        copy('status');
        if (patch['status'] === undefined) patch['status'] = 'PENDING';
        copy('createdAt');
        copy('invitationSent');
        patch['startedAt'] = null;
        patch['completedAt'] = null;
        patch['questionScore'] = null;
        patch['answerScore'] = null;
        patch['answerTotal'] = null;
        patch['percentage'] = null;
        patch['resultsSent'] = false;
        patch['language'] = null;
        patch['selectedAnswerIds'] = [];
        break;
      case 'RefTestRevived':
        copy('status');
        if (patch['status'] === undefined) patch['status'] = 'PENDING';
        copy('createdAt');
        copy('invitationSent');
        if (patch['invitationSent'] === undefined) patch['invitationSent'] = false;
        break;
      case 'RefTestCreated': {
        copy('name');
        copy('firstName');
        copy('lastName');
        copy('email');
        copy('invitationSent');
        copy('resultsSent');
        copy('sendInvitationsAutomatically');
        copy('sendResultsAutomatically');
        copy('status');
        copy('createdAt');
        copy('scheduledAt');
        copy('numberOfQuestions');
        copy('maxTimeInMinutes');
        if (event['titleId'] !== undefined) {
          patch['title'] =
            event['titleId'] === null
              ? null
              : {
                  __typename: 'RefTestTitleDto',
                  id: event['titleId'],
                  value: event['titleValue'] ?? '',
                };
        }
        break;
      }
      case 'RefTestApproved':
        copy('status');
        copy('createdAt');
        patch['rejectionReason'] = null;
        break;
      case 'RefTestRejected':
        copy('status');
        copy('reason', 'rejectionReason');
        break;
      default:
        return null;
    }

    return patch;
  }
}
