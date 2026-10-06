import {
  GetRefTestsQuery,
  RefTestStatus,
} from '../../../../graphql/generated';
import { PERCENTAGE_RANGES } from '../list/services/constants';
import { IRefTestFilter } from '../list/services/types';

export type RefTestCacheSnapshot = {
  readonly __typename: 'RefTest';
  readonly id: string;
  [field: string]: unknown;
};

export type RefTestCacheListStatus = RefTestStatus | undefined;

type FilterMatch = boolean | undefined;
/** Cached CreatedAt values (by RefTest id) the server replaced; they cannot place a row. */
export type RefTestStaleCreatedAt = ReadonlyMap<string, unknown>;
type CachedRefTestEdge = NonNullable<
  NonNullable<NonNullable<GetRefTestsQuery['refTests']>['edges']>[number]
>;

export const CACHED_REF_TEST_STATUSES: readonly RefTestStatus[] = [
  'PENDING',
  'IN_PROGRESS',
  'COMPLETED',
  'EXPIRED',
  'PENDING_APPROVAL',
  'REJECTED',
];

export const CACHED_REF_TEST_LIST_STATUSES: readonly RefTestCacheListStatus[] = [
  undefined,
  ...CACHED_REF_TEST_STATUSES,
];

export type RefTestCachePlan = {
  readonly lists: ReadonlyMap<RefTestCacheListStatus, GetRefTestsQuery>;
  readonly countDeltas: ReadonlyMap<RefTestCacheListStatus, number>;
  readonly invalidateLists: boolean;
  readonly invalidateCounts: boolean;
};

type RefTestCachePlanInput = {
  readonly cachedLists: ReadonlyMap<RefTestCacheListStatus, GetRefTestsQuery | null>;
  readonly id: string;
  readonly before: RefTestCacheSnapshot | null;
  readonly after: RefTestCacheSnapshot | null;
  readonly patch: Record<string, unknown>;
  readonly beforeIsAbsent: boolean;
  readonly afterIsAbsent: boolean;
  readonly isDeleted: boolean;
  readonly filter: IRefTestFilter;
  readonly staleCreatedAt: RefTestStaleCreatedAt;
};

/** Plans list-edge and count changes without performing Apollo cache writes. */
export function planRefTestCacheUpdate({
  cachedLists,
  id,
  before,
  after,
  patch,
  beforeIsAbsent,
  afterIsAbsent,
  isDeleted,
  filter,
  staleCreatedAt,
}: RefTestCachePlanInput): RefTestCachePlan {
  const countDeltas = getListCountDeltas(
    id,
    before,
    after,
    beforeIsAbsent,
    afterIsAbsent,
    cachedLists,
    filter,
  );
  if (beforeIsAbsent) {
    // A create event has no server edge cursor. Do not advertise a new total for a row
    // that cannot be represented safely in the loaded connection window.
    for (const [status, delta] of countDeltas) {
      if (delta > 0) countDeltas.set(status, 0);
    }
  }
  const invalidateCounts =
    isDeleted ||
    beforeIsAbsent ||
    afterIsAbsent ||
    hasChangedFields(before, after, COUNT_FILTER_FIELDS) ||
    [...countDeltas.values()].some((delta) => delta !== 0);
  const invalidateLists =
    invalidateCounts ||
    isDeleted ||
    beforeIsAbsent ||
    afterIsAbsent ||
    hasChangedFields(before, after, ORDER_FIELDS) ||
    Boolean(
      after &&
        staleCreatedAt.get(after.id) !== undefined &&
        staleCreatedAt.get(after.id) === after['createdAt'],
    );
  const lists = invalidateLists
    ? new Map<RefTestCacheListStatus, GetRefTestsQuery>()
    : planUpdatedLists(cachedLists, id, patch);

  return { lists, countDeltas, invalidateLists, invalidateCounts };
}

export function getRefTestCacheStatus(status: unknown): RefTestStatus | undefined {
  return CACHED_REF_TEST_STATUSES.find((candidate) => candidate === status);
}

const COUNT_FILTER_FIELDS = [
  'status',
  'email',
  'firstName',
  'lastName',
  'title',
  'invitationSent',
  'resultsSent',
  'isAnonymized',
  'language',
  'questionScore',
  'answerScore',
  'percentage',
  'numberOfQuestions',
  'maxTimeInMinutes',
  'startedAt',
  'completedAt',
  'scheduledAt',
];

const ORDER_FIELDS = [
  'title',
  'completedAt',
  'startedAt',
  'scheduledAt',
  'email',
  'firstName',
  'lastName',
  'questionScore',
  'answerScore',
  'percentage',
  'status',
  'numberOfQuestions',
  'invitationSent',
  'resultsSent',
  'maxTimeInMinutes',
  'createdAt',
];

function hasChangedFields(
  before: RefTestCacheSnapshot | null,
  after: RefTestCacheSnapshot | null,
  fields: readonly string[],
): boolean {
  for (const field of fields) {
    const beforeValue = readComparableField(before, field);
    const afterValue = readComparableField(after, field);
    if (beforeValue === undefined && afterValue === undefined) continue;
    if (beforeValue === undefined || afterValue === undefined || beforeValue !== afterValue) {
      return true;
    }
  }
  return false;
}

function readComparableField(snapshot: RefTestCacheSnapshot | null, field: string): unknown {
  if (!snapshot) return undefined;
  return field === 'title' ? titleValue(snapshot) : snapshot[field];
}

function getListCountDeltas(
  id: string,
  before: RefTestCacheSnapshot | null,
  after: RefTestCacheSnapshot | null,
  beforeIsAbsent: boolean,
  afterIsAbsent: boolean,
  cachedLists: ReadonlyMap<RefTestCacheListStatus, GetRefTestsQuery | null>,
  filter: IRefTestFilter,
): Map<RefTestCacheListStatus, number> {
  const deltas = new Map<RefTestCacheListStatus, number>();
  const sourceNode = findCachedEdge(cachedLists, id)?.node;
  const knownBeforeSearchMatch = hasSameSearchFields(sourceNode, before, filter) ? true : undefined;
  const knownAfterSearchMatch = hasSameSearchFields(sourceNode, after, filter) ? true : undefined;

  for (const status of CACHED_REF_TEST_LIST_STATUSES) {
    const previousEdges = cachedLists.get(status)?.refTests?.edges ?? [];
    const loadedEdge = previousEdges.find((edge) => edge.node.id === id);
    const edgeWasLoaded = loadedEdge !== undefined;
    const beforeMatch = edgeWasLoaded
      ? true
      : matchesList(before, status, filter, beforeIsAbsent, knownBeforeSearchMatch);
    const afterMatch = matchesList(
      after,
      status,
      filter,
      afterIsAbsent,
      knownAfterSearchMatch,
    );
    deltas.set(status, membershipDelta(beforeMatch, afterMatch));
  }

  return deltas;
}

function matchesList(
  snapshot: RefTestCacheSnapshot | null,
  status: RefTestCacheListStatus,
  filter: IRefTestFilter,
  isAbsent: boolean,
  knownSearchMatch?: FilterMatch,
): FilterMatch {
  if (isAbsent) return false;
  if (!snapshot) return undefined;

  if (status !== undefined) {
    const snapshotStatus = snapshot['status'];
    if (snapshotStatus === undefined) return undefined;
    if (snapshotStatus !== status) return false;
  }

  return matchesNonStatusFilters(snapshot, filter, knownSearchMatch);
}

function matchesNonStatusFilters(
  snapshot: RefTestCacheSnapshot,
  filter: IRefTestFilter,
  knownSearchMatch?: FilterMatch,
): FilterMatch {
  const matches: FilterMatch[] = [];

  if (filter.searchTerm) {
    matches.push(
      knownSearchMatch ??
        anyMatch([
          matchesContains(snapshot['email'], filter.searchTerm),
          matchesContains(snapshot['firstName'], filter.searchTerm),
          matchesContains(snapshot['lastName'], filter.searchTerm),
        ]),
    );
  }

  if (filter.titleValue !== undefined) {
    const title = snapshot['title'];
    const titleValue =
      typeof title === 'object' && title !== null
        ? (title as Record<string, unknown>)['value']
        : title;
    matches.push(matchesEqual(titleValue, filter.titleValue));
  }

  if (filter.invitationSent !== undefined) {
    matches.push(matchesEqual(snapshot['invitationSent'], filter.invitationSent));
  }
  if (filter.resultsSent !== undefined) {
    matches.push(matchesEqual(snapshot['resultsSent'], filter.resultsSent));
  }
  if (filter.isAnonymized !== undefined) {
    matches.push(matchesEqual(snapshot['isAnonymized'], filter.isAnonymized));
  }
  if (filter.language !== undefined) {
    matches.push(matchesEqual(snapshot['language'], filter.language));
  }

  matches.push(
    matchesNumberRange(
      snapshot['questionScore'],
      filter.minQuestionScore,
      filter.maxQuestionScore,
    ),
    matchesNumberRange(
      snapshot['answerScore'],
      filter.minAnswerScore,
      filter.maxAnswerScore,
    ),
    matchesNumberRange(
      snapshot['numberOfQuestions'],
      filter.minQuestions,
      filter.maxQuestions,
    ),
    matchesNumberRange(
      snapshot['maxTimeInMinutes'],
      filter.minMaxTimeInMinutes,
      filter.maxMaxTimeInMinutes,
    ),
    matchesDateRange(snapshot['startedAt'], filter.startedAfter, filter.startedBefore),
    matchesDateRange(snapshot['completedAt'], filter.completedAfter, filter.completedBefore),
    matchesDateRange(snapshot['scheduledAt'], filter.scheduledAfter, filter.scheduledBefore),
  );

  if (filter.percentageRange) {
    const percentage = snapshot['percentage'];
    if (percentage === undefined) matches.push(undefined);
    else if (typeof percentage !== 'number') matches.push(false);
    else if (filter.percentageRange === 'low') {
      matches.push(percentage < PERCENTAGE_RANGES.LOW.max);
    } else if (filter.percentageRange === 'medium') {
      matches.push(
        percentage >= PERCENTAGE_RANGES.MEDIUM.min && percentage < PERCENTAGE_RANGES.MEDIUM.max,
      );
    } else {
      matches.push(percentage >= PERCENTAGE_RANGES.HIGH.min);
    }
  }

  return allMatch(matches);
}

function matchesEqual(value: unknown, expected: unknown): FilterMatch {
  if (value === undefined) return undefined;
  return value === expected;
}

function matchesContains(value: unknown, term: string): FilterMatch {
  if (value === undefined) return undefined;
  if (typeof value !== 'string') return false;
  if (value.includes(term)) return true;

  if (
    normalizeForSearch(value).includes(normalizeForSearch(term)) ||
    /[^\u0000-\u007f]/.test(value) ||
    /[^\u0000-\u007f]/.test(term)
  ) {
    return undefined;
  }

  return false;
}

function normalizeForSearch(value: string): string {
  return value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

function hasSameSearchFields(
  previous: Readonly<{ email?: unknown; firstName?: unknown; lastName?: unknown }> | undefined,
  next: RefTestCacheSnapshot | null,
  filter: IRefTestFilter,
): boolean {
  if (!filter.searchTerm || !previous || !next) return false;
  return (
    previous.email !== undefined &&
    previous.firstName !== undefined &&
    previous.lastName !== undefined &&
    previous.email === next['email'] &&
    previous.firstName === next['firstName'] &&
    previous.lastName === next['lastName']
  );
}

function matchesNumberRange(
  value: unknown,
  min: number | undefined,
  max: number | undefined,
): FilterMatch {
  if (min === undefined && max === undefined) return true;
  if (value === undefined) return undefined;
  if (typeof value !== 'number') return false;
  return (min === undefined || value >= min) && (max === undefined || value <= max);
}

function matchesDateRange(
  value: unknown,
  after: string | undefined,
  before: string | undefined,
): FilterMatch {
  if (!after && !before) return true;
  if (value === undefined) return undefined;
  if (typeof value !== 'string') return false;

  const valueTime = Date.parse(value);
  const afterTime = after ? Date.parse(after) : undefined;
  const beforeTime = before ? Date.parse(before) : undefined;
  if (
    Number.isNaN(valueTime) ||
    (afterTime !== undefined && Number.isNaN(afterTime)) ||
    (beforeTime !== undefined && Number.isNaN(beforeTime))
  ) {
    return undefined;
  }

  return (
    (afterTime === undefined || valueTime >= afterTime) &&
    (beforeTime === undefined || valueTime <= beforeTime)
  );
}

function allMatch(matches: readonly FilterMatch[]): FilterMatch {
  if (matches.includes(false)) return false;
  if (matches.includes(undefined)) return undefined;
  return true;
}

function anyMatch(matches: readonly FilterMatch[]): FilterMatch {
  if (matches.includes(true)) return true;
  if (matches.includes(undefined)) return undefined;
  return false;
}

function membershipDelta(before: FilterMatch, after: FilterMatch): number {
  if (before === undefined || after === undefined || before === after) return 0;
  return after ? 1 : -1;
}

function planUpdatedLists(
  cachedLists: ReadonlyMap<RefTestCacheListStatus, GetRefTestsQuery | null>,
  id: string,
  patch: Record<string, unknown>,
): Map<RefTestCacheListStatus, GetRefTestsQuery> {
  const updates = new Map<RefTestCacheListStatus, GetRefTestsQuery>();
  if (Object.keys(patch).length === 0) return updates;

  for (const status of CACHED_REF_TEST_LIST_STATUSES) {
    const previous = cachedLists.get(status);
    const connection = previous?.refTests;
    if (!previous || !connection) continue;

    let edgeChanged = false;
    const edges = (connection.edges ?? []).map((edge) => {
      if (edge.node.id !== id) return edge;
      edgeChanged = true;
      return {
        ...edge,
        node: { ...edge.node, ...patch } as (typeof edge)['node'],
      };
    });
    if (!edgeChanged) continue;

    updates.set(status, {
      ...previous,
      refTests: {
        ...connection,
        edges,
      },
    });
  }

  return updates;
}

function findCachedEdge(
  cachedLists: ReadonlyMap<RefTestCacheListStatus, GetRefTestsQuery | null>,
  id: string,
): CachedRefTestEdge | null {
  for (const list of cachedLists.values()) {
    const edge = list?.refTests?.edges?.find((candidate) => candidate.node.id === id);
    if (edge) return edge;
  }
  return null;
}

function titleValue(snapshot: RefTestCacheSnapshot): unknown {
  const title = snapshot['title'];
  if (title === undefined || title === null) return title;
  return typeof title === 'object' ? (title as Record<string, unknown>)['value'] : undefined;
}
