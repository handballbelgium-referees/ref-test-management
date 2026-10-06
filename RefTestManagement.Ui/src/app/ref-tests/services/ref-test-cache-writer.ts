import { inject, Service } from '@angular/core';
import { ApolloCache, gql } from '@apollo/client';
import {
  GetRefTestsAllCountsGQL,
  GetRefTestsAllCountsQuery,
  GetRefTestsGQL,
  GetRefTestsQuery,
} from '../../../../graphql/generated';
import { RefTestFilterState } from '../list/services/ref-test-filter-state';
import { RefTestQueryBuilder } from '../list/services/ref-test-query-builder';
import {
  CACHED_REF_TEST_LIST_STATUSES,
  RefTestCacheListStatus,
  RefTestCachePlan,
  RefTestCacheSnapshot,
} from './ref-test-cache-planner';

const REF_TEST_CACHE_SNAPSHOT = gql`
  fragment RefTestCacheSnapshot on RefTest {
    __typename
    id
    firstName
    lastName
    name
    email
    title {
      id
      value
    }
    invitationSent
    resultsSent
    sendInvitationsAutomatically
    sendResultsAutomatically
    status
    createdAt
    numberOfQuestions
    maxTimeInMinutes
    scheduledAt
    startedAt
    completedAt
    questionScore
    answerScore
    questionTotal
    answerTotal
    percentage
    language
    rejectionReason
    isAnonymized
  }
`;

const REF_TEST_CREATED_CACHE_SNAPSHOT = gql`
  fragment RefTestCreatedCacheSnapshot on RefTest {
    __typename
    id
    name
    email
    title {
      id
      value
    }
    invitationSent
    resultsSent
    sendInvitationsAutomatically
    sendResultsAutomatically
    status
    numberOfQuestions
    maxTimeInMinutes
  }
`;

function hasOrderArgument(storeFieldName: string): boolean {
  const opening = storeFieldName.indexOf('(');
  if (opening < 0 || !storeFieldName.endsWith(')')) return false;

  try {
    const args = JSON.parse(storeFieldName.slice(opening + 1, -1)) as unknown;
    return typeof args === 'object' && args !== null && Object.hasOwn(args, 'order');
  } catch {
    return false;
  }
}

type RefTestCacheSnapshotRead = {
  readonly entityId: string | undefined;
  readonly snapshot: RefTestCacheSnapshot | null;
};

type RefTestCacheWriteInput = {
  readonly id: string;
  readonly entityId: string | undefined;
  readonly after: RefTestCacheSnapshot | null;
  readonly patch: Record<string, unknown>;
  readonly eventType: string;
  readonly isDeleted: boolean;
  readonly plan: RefTestCachePlan;
};

/** Reads and writes the Apollo cache for RefTest subscription reconciliation. */
@Service()
export class RefTestCacheWriter {
  private readonly _getRefTestsGQL = inject(GetRefTestsGQL);
  private readonly _getRefTestsAllCountsGQL = inject(GetRefTestsAllCountsGQL);
  private readonly _filterState = inject(RefTestFilterState);
  private readonly _queryBuilder = inject(RefTestQueryBuilder);

  public buildCountsVariables() {
    const base = this._queryBuilder.buildWhereFilter(this._filterState.filter(), {
      excludeStatus: true,
    });

    return {
      allWhere: base,
      pendingWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'PENDING' },
      }),
      inProgressWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'IN_PROGRESS' },
      }),
      completedWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'COMPLETED' },
      }),
      expiredWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'EXPIRED' },
      }),
      pendingApprovalWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'PENDING_APPROVAL' },
      }),
      rejectedWhere: this._queryBuilder.mergeFilters(base, {
        status: { eq: 'REJECTED' },
      }),
    };
  }

  public readSnapshot(cache: ApolloCache, id: string): RefTestCacheSnapshotRead {
    const entityId = cache.identify({ __typename: 'RefTest', id });
    const snapshot = entityId
      ? cache.readFragment<RefTestCacheSnapshot>({
          id: entityId,
          fragment: REF_TEST_CACHE_SNAPSHOT,
          returnPartialData: true,
        })
      : null;
    return { entityId, snapshot };
  }

  public readCachedLists(
    cache: ApolloCache,
  ): Map<RefTestCacheListStatus, GetRefTestsQuery | null> {
    const lists = new Map<RefTestCacheListStatus, GetRefTestsQuery | null>();
    for (const status of CACHED_REF_TEST_LIST_STATUSES) {
      lists.set(status, this.readCachedList(cache, status));
    }
    return lists;
  }

  public hasCachedCounts(cache: ApolloCache): boolean {
    return Boolean(
      cache.readQuery<GetRefTestsAllCountsQuery>({
        query: this._getRefTestsAllCountsGQL.document,
        variables: this.buildCountsVariables(),
      }),
    );
  }

  public applyPlan(cache: ApolloCache, input: RefTestCacheWriteInput): void {
    cache.batch({
      update: (batchedCache) => {
        if (input.isDeleted) {
          batchedCache.evict({
            id: 'ROOT_QUERY',
            fieldName: 'refTest',
            args: { id: input.id },
          });
          if (input.entityId) batchedCache.evict({ id: input.entityId });
        } else {
          this.writeNormalizedRefTest(
            batchedCache,
            input.entityId,
            input.after,
            input.patch,
            input.eventType,
          );
        }

        if (input.plan.invalidateLists || input.plan.invalidateCounts) {
          batchedCache.modify({
            id: 'ROOT_QUERY',
            fields: {
              refTests(existing, { storeFieldName, DELETE }) {
                const isListConnection = hasOrderArgument(storeFieldName);
                const shouldInvalidate = isListConnection
                  ? input.plan.invalidateLists
                  : input.plan.invalidateCounts;
                return shouldInvalidate ? DELETE : existing;
              },
            },
          });
        }

        if (!input.plan.invalidateLists) {
          for (const [status, data] of input.plan.lists) {
            this.writeCachedList(batchedCache, status, data);
          }
        }
        if (!input.plan.invalidateCounts) {
          this.writeCountsCache(batchedCache, input.plan.countDeltas);
        }
      },
    });
    if (input.isDeleted || input.plan.invalidateLists || input.plan.invalidateCounts) cache.gc();
  }

  private buildListVariables(status: RefTestCacheListStatus) {
    const filter = this._filterState.filter();
    const base = this._queryBuilder.buildWhereFilter(filter, { excludeStatus: true });
    const where = status ? this._queryBuilder.mergeFilters(base, { status: { eq: status } }) : base;
    return {
      first: filter.pagingInfo.first,
      where,
      order: this._queryBuilder.buildOrderClause(filter),
    };
  }

  private readCachedList(
    cache: ApolloCache,
    status: RefTestCacheListStatus,
  ): GetRefTestsQuery | null {
    return cache.readQuery<GetRefTestsQuery>({
      query: this._getRefTestsGQL.document,
      variables: this.buildListVariables(status),
    });
  }

  private writeCachedList(
    cache: ApolloCache,
    status: RefTestCacheListStatus,
    data: GetRefTestsQuery,
  ): void {
    cache.writeQuery({
      query: this._getRefTestsGQL.document,
      variables: this.buildListVariables(status),
      data,
    });
  }

  private writeNormalizedRefTest(
    cache: ApolloCache,
    entityId: string | undefined,
    after: RefTestCacheSnapshot | null,
    patch: Record<string, unknown>,
    eventType: string,
  ): void {
    if (!entityId || !after || Object.keys(patch).length === 0) return;

    const hasCachedEntity = (cache.extract() as Record<string, unknown>)[entityId] !== undefined;
    if (!hasCachedEntity) {
      if (eventType === 'RefTestCreated') {
        cache.writeFragment({
          id: entityId,
          fragment: REF_TEST_CREATED_CACHE_SNAPSHOT,
          data: after,
        });
      }
      return;
    }

    const fields: Record<string, () => unknown> = {};
    for (const [field, value] of Object.entries(patch)) {
      fields[field] = () => value;
    }
    cache.modify({ id: entityId, fields });
  }

  private writeCountsCache(
    cache: ApolloCache,
    deltas: ReadonlyMap<RefTestCacheListStatus, number>,
  ): void {
    const variables = this.buildCountsVariables();
    const previous = cache.readQuery<GetRefTestsAllCountsQuery>({
      query: this._getRefTestsAllCountsGQL.document,
      variables,
    });
    if (!previous) return;

    const allDelta = deltas.get(undefined) ?? 0;
    const pendingDelta = deltas.get('PENDING') ?? 0;
    const inProgressDelta = deltas.get('IN_PROGRESS') ?? 0;
    const completedDelta = deltas.get('COMPLETED') ?? 0;
    const expiredDelta = deltas.get('EXPIRED') ?? 0;
    const pendingApprovalDelta = deltas.get('PENDING_APPROVAL') ?? 0;
    const rejectedDelta = deltas.get('REJECTED') ?? 0;
    if (
      allDelta === 0 &&
      pendingDelta === 0 &&
      inProgressDelta === 0 &&
      completedDelta === 0 &&
      expiredDelta === 0 &&
      pendingApprovalDelta === 0 &&
      rejectedDelta === 0
    ) {
      return;
    }

    cache.writeQuery({
      query: this._getRefTestsAllCountsGQL.document,
      variables,
      data: {
        all: this.applyCountDelta(previous.all, allDelta),
        pending: this.applyCountDelta(previous.pending, pendingDelta),
        inProgress: this.applyCountDelta(previous.inProgress, inProgressDelta),
        completed: this.applyCountDelta(previous.completed, completedDelta),
        expired: this.applyCountDelta(previous.expired, expiredDelta),
        pendingApproval: this.applyCountDelta(previous.pendingApproval, pendingApprovalDelta),
        rejected: this.applyCountDelta(previous.rejected, rejectedDelta),
      },
    });
  }

  private applyCountDelta(
    count: GetRefTestsAllCountsQuery['all'],
    delta: number,
  ): GetRefTestsAllCountsQuery['all'] {
    if (!count || delta === 0) return count;
    return { ...count, totalCount: Math.max(0, count.totalCount + delta) };
  }
}
