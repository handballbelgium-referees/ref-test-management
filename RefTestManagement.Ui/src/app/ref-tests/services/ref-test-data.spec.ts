import { DestroyRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApolloCache, ApolloClient, ApolloLink, gql } from '@apollo/client';
import { Router } from '@angular/router';
import { Apollo } from 'apollo-angular';
import type { DocumentNode } from 'graphql';
import { BehaviorSubject, firstValueFrom, NEVER, Observable, Subject, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ApproveRefTestsDocument,
  ApproveRefTestsGQL,
  DeleteRefTestsGQL,
  ExtendRefTestTimeGQL,
  GetRefTestByIdGQL,
  GetRefTestsAllCountsGQL,
  GetRefTestsGQL,
  RefTestStatus,
  RefTestUpdatedGQL,
  RefTestsUpdatedGQL,
  RegenerateRefTestTokenGQL,
  RejectRefTestsDocument,
  RejectRefTestsGQL,
  ResetRefTestsDocument,
  ResetRefTestsGQL,
  ReviveRefTestsDocument,
  ReviveRefTestsGQL,
  SendRefTestInvitationsGQL,
  SendRefTestResultsGQL,
  SendReportGQL,
  UpdateRefTestConfigurationGQL,
  UpdateRefTestDetailsGQL,
  UpdateRefTestNotificationSettingsGQL,
} from '../../../../graphql/generated';
import { createAppApolloCache } from '../../app.config';
import { ErrorReporter } from '../../services/error-reporter';
import { PermissionsService } from '../../auth/services/permissions';
import { RefTestDetailData } from '../detail/services/ref-test-detail-data';
import { RefTestFilterState } from '../list/services/ref-test-filter-state';
import { RefTestQueryBuilder } from '../list/services/ref-test-query-builder';
import { IRefTestFilter } from '../list/services/types';
import { RefTestCacheUpdater } from './ref-test-cache-updater';
import { RefTestData } from './ref-test-data';

const REF_TESTS_QUERY = gql`
  query RefTestsCacheTest(
    $first: Int
    $after: String
    $where: RefTestFilterInput
    $order: [RefTestSortInput!]
  ) {
    refTests(first: $first, after: $after, where: $where, order: $order) {
      edges {
        cursor
        node {
          id
          title {
            id
            value
          }
          firstName
          lastName
          name
          email
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
      }
      pageInfo {
        hasNextPage
        endCursor
      }
      totalCount
    }
  }
`;

const REF_TEST_COUNTS_QUERY = gql`
  query RefTestsCountsCacheTest(
    $allWhere: RefTestFilterInput
    $pendingWhere: RefTestFilterInput
    $inProgressWhere: RefTestFilterInput
    $completedWhere: RefTestFilterInput
    $expiredWhere: RefTestFilterInput
    $pendingApprovalWhere: RefTestFilterInput
    $rejectedWhere: RefTestFilterInput
  ) {
    all: refTests(first: 0, where: $allWhere) {
      totalCount
    }
    pending: refTests(first: 0, where: $pendingWhere) {
      totalCount
    }
    inProgress: refTests(first: 0, where: $inProgressWhere) {
      totalCount
    }
    completed: refTests(first: 0, where: $completedWhere) {
      totalCount
    }
    expired: refTests(first: 0, where: $expiredWhere) {
      totalCount
    }
    pendingApproval: refTests(first: 0, where: $pendingApprovalWhere) {
      totalCount
    }
    rejected: refTests(first: 0, where: $rejectedWhere) {
      totalCount
    }
  }
`;

const REF_TEST_STATE_FRAGMENT = gql`
  fragment RefTestStateForCacheTest on RefTest {
    id
    resultsSent
  }
`;

const REF_TEST_REPLAY_BEFORE_FRAGMENT = gql`
  fragment RefTestReplayBeforeCacheTest on RefTest {
    id
    status
    resultsSent
  }
`;

const REF_TEST_DETAIL_QUERY = gql`
  query RefTestDetailCacheTest($id: ID!) {
    refTest(id: $id) {
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
  }
`;

const REF_TEST_RESET_FRAGMENT = gql`
  fragment RefTestResetCacheTest on RefTest {
    id
    status
    invitationSent
    resultsSent
    questionTotal
    answerTotal
    percentage
    language
    createdAt
  }
`;

const REF_TEST_ANONYMIZED_FRAGMENT = gql`
  fragment RefTestAnonymizedForCacheTest on RefTest {
    id
    firstName
    lastName
    name
    email
    isAnonymized
    rejectionReason
  }
`;

const REF_TEST_SELECTIONS_FRAGMENT = gql`
  fragment RefTestSelectionsCacheTest on RefTest {
    id
    selectedAnswerIds
  }
`;

const REF_TEST_CREATED_AT_FRAGMENT = gql`
  fragment RefTestCreatedAtCacheTest on RefTest {
    id
    createdAt
  }
`;

type TestRefTest = {
  __typename: 'RefTest';
  id: string;
  firstName: string;
  lastName: string;
  name: string;
  email: string;
  invitationSent: boolean;
  resultsSent: boolean;
  sendInvitationsAutomatically: boolean;
  sendResultsAutomatically: boolean;
  status: RefTestStatus;
  createdAt: string;
  numberOfQuestions: number;
  maxTimeInMinutes: number;
  scheduledAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  questionScore: number | null;
  answerScore: number | null;
  questionTotal: number | null;
  answerTotal: number | null;
  percentage: number | null;
  language: string | null;
  rejectionReason: string | null;
  isAnonymized: boolean;
  title: { __typename: 'RefTestTitleDto'; id: string; value: string } | null;
};

type TestEdge = { cursor: string; node: TestRefTest };
type TestConnection = {
  edges: TestEdge[];
  totalCount: number;
  pageInfo: { hasNextPage: boolean; endCursor: string | null };
};
type TestListData = { refTests: TestConnection | null };
type TestCount = { totalCount: number };
type TestCountsData = {
  all: TestCount | null;
  pending: TestCount | null;
  inProgress: TestCount | null;
  completed: TestCount | null;
  expired: TestCount | null;
  pendingApproval: TestCount | null;
  rejected: TestCount | null;
};
type TestSubscriptionEvent = {
  __typename: string;
  id: string;
  [field: string]: unknown;
};
type ListQueryResult = { data?: TestListData; loading?: boolean };
type CountsQueryResult = { data?: TestCountsData; loading?: boolean };
type DetailQueryResult = { data?: { refTest?: TestRefTest }; loading: boolean };
type ListSubscriptionResult = { data?: { refTestsUpdated: TestSubscriptionEvent } };
type DetailSubscriptionResult = { data?: { refTestUpdated: TestSubscriptionEvent } };

type TestHarness = {
  cache: ReturnType<typeof createAppApolloCache>;
  cacheUpdater: RefTestCacheUpdater;
  data: RefTestData;
  detailData: RefTestDetailData;
  detailEvents: Subject<DetailSubscriptionResult>;
  detailQueryResults: BehaviorSubject<DetailQueryResult>;
  detailQueryRef: { refetch: ReturnType<typeof vi.fn> };
  detailSubscribe: ReturnType<typeof vi.fn>;
  detailWatch: ReturnType<typeof vi.fn>;
  filterState: RefTestFilterState;
  listEvents: Subject<ListSubscriptionResult>;
  listQueryResults: Subject<ListQueryResult>;
  countsQueryResults: Subject<CountsQueryResult>;
  listQueryRef: {
    fetchMore: ReturnType<typeof vi.fn>;
    refetch: ReturnType<typeof vi.fn>;
    setVariables: ReturnType<typeof vi.fn>;
  };
  countsQueryRef: {
    refetch: ReturnType<typeof vi.fn>;
    setVariables: ReturnType<typeof vi.fn>;
  };
  networkRequests: () => number;
  queryBuilder: RefTestQueryBuilder;
};

function makeRefTest(
  id: string,
  overrides: Partial<TestRefTest> = {},
): TestRefTest {
  return {
    __typename: 'RefTest',
    id,
    firstName: `First${id}`,
    lastName: `Last${id}`,
    name: `First${id} Last${id}`,
    email: `${id}@example.test`,
    invitationSent: false,
    resultsSent: false,
    sendInvitationsAutomatically: false,
    sendResultsAutomatically: false,
    status: 'PENDING',
    createdAt: '2026-10-01T10:00:00.000Z',
    numberOfQuestions: 10,
    maxTimeInMinutes: 30,
    scheduledAt: null,
    startedAt: null,
    completedAt: null,
    questionScore: null,
    answerScore: null,
    questionTotal: 10,
    answerTotal: null,
    percentage: null,
    language: null,
    rejectionReason: null,
    isAnonymized: false,
    title: { __typename: 'RefTestTitleDto', id: 'title-1', value: 'Title' },
    ...overrides,
  };
}

function createHarness(
  filter: Partial<IRefTestFilter> = {},
  detailTest = makeRefTest('detail'),
): TestHarness {
  const cache = createAppApolloCache();
  let networkRequestCount = 0;
  const client = new ApolloClient({
    cache,
    link: new ApolloLink(
      () =>
        new Observable(() => {
          networkRequestCount++;
        }),
    ),
  });
  const listEvents = new Subject<ListSubscriptionResult>();
  const listQueryResults = new Subject<ListQueryResult>();
  const countsQueryResults = new Subject<CountsQueryResult>();
  const detailEvents = new Subject<DetailSubscriptionResult>();
  let currentListQueryResult: ListQueryResult = { loading: false };
  let currentCountsQueryResult: CountsQueryResult = { loading: false };
  listQueryResults.subscribe((result) => {
    currentListQueryResult = result;
  });
  countsQueryResults.subscribe((result) => {
    currentCountsQueryResult = result;
  });
  const listQueryRef = {
    variables: {},
    setVariables: vi.fn(),
    valueChanges: listQueryResults.asObservable(),
    getCurrentResult: () => currentListQueryResult,
    fetchMore: vi.fn(() => Promise.resolve({ data: {} })),
    refetch: vi.fn(() => Promise.resolve({ data: {} })),
  };
  const countsQueryRef = {
    variables: {},
    setVariables: vi.fn(),
    valueChanges: countsQueryResults.asObservable(),
    getCurrentResult: () => currentCountsQueryResult,
    refetch: vi.fn(() => Promise.resolve({ data: {} })),
  };
  const detailValueChanges = new BehaviorSubject<DetailQueryResult>({
    data: { refTest: detailTest },
    loading: false,
  });
  const detailQueryRef = {
    valueChanges: detailValueChanges.asObservable(),
    getCurrentResult: () => detailValueChanges.value,
    refetch: vi.fn(),
  };
  const detailWatch = vi.fn(() => detailQueryRef);
  const detailSubscribe = vi.fn(() => detailEvents);
  const emptyGql = { mutate: vi.fn() };

  TestBed.configureTestingModule({
    providers: [
      RefTestCacheUpdater,
      RefTestData,
      RefTestDetailData,
      RefTestFilterState,
      RefTestQueryBuilder,
      { provide: Apollo, useValue: { client } },
      {
        provide: GetRefTestsGQL,
        useValue: {
          document: REF_TESTS_QUERY,
          watch: vi.fn(() => listQueryRef),
        },
      },
      {
        provide: GetRefTestsAllCountsGQL,
        useValue: {
          document: REF_TEST_COUNTS_QUERY,
          watch: vi.fn(() => countsQueryRef),
        },
      },
      { provide: ApproveRefTestsGQL, useValue: emptyGql },
      { provide: DeleteRefTestsGQL, useValue: emptyGql },
      { provide: ExtendRefTestTimeGQL, useValue: emptyGql },
      {
        provide: GetRefTestByIdGQL,
        useValue: { watch: detailWatch },
      },
      { provide: RefTestUpdatedGQL, useValue: { subscribe: detailSubscribe } },
      { provide: RefTestsUpdatedGQL, useValue: { subscribe: vi.fn(() => listEvents) } },
      { provide: RegenerateRefTestTokenGQL, useValue: emptyGql },
      { provide: RejectRefTestsGQL, useValue: emptyGql },
      { provide: ResetRefTestsGQL, useValue: emptyGql },
      { provide: ReviveRefTestsGQL, useValue: emptyGql },
      { provide: SendRefTestInvitationsGQL, useValue: emptyGql },
      { provide: SendRefTestResultsGQL, useValue: emptyGql },
      { provide: SendReportGQL, useValue: emptyGql },
      { provide: UpdateRefTestConfigurationGQL, useValue: emptyGql },
      { provide: UpdateRefTestDetailsGQL, useValue: emptyGql },
      { provide: UpdateRefTestNotificationSettingsGQL, useValue: emptyGql },
      { provide: ErrorReporter, useValue: { report: vi.fn() } },
      { provide: PermissionsService, useValue: { hasPermission: () => false } },
      { provide: Router, useValue: { navigate: vi.fn() } },
    ],
  });

  const filterState = TestBed.inject(RefTestFilterState);
  filterState.updateFilter({
    searchTerm: '',
    sortField: 'numberOfQuestions',
    sortDirection: 'DESC',
    pagingInfo: { first: 20 },
    ...filter,
  });

  return {
    cache,
    cacheUpdater: TestBed.inject(RefTestCacheUpdater),
    data: TestBed.inject(RefTestData),
    detailData: TestBed.inject(RefTestDetailData),
    detailEvents,
    detailQueryResults: detailValueChanges,
    detailQueryRef,
    detailSubscribe,
    detailWatch,
    filterState,
    listEvents,
    listQueryResults,
    countsQueryResults,
    listQueryRef,
    countsQueryRef,
    networkRequests: () => networkRequestCount,
    queryBuilder: TestBed.inject(RefTestQueryBuilder),
  };
}

function testDestroyRef(): DestroyRef {
  return {
    destroyed: false,
    onDestroy: () => () => {},
  } as unknown as DestroyRef;
}

function listVariables(
  harness: TestHarness,
  status?: RefTestStatus,
  after?: string,
) {
  const filter = harness.filterState.filter();
  const base = harness.queryBuilder.buildWhereFilter(filter, { excludeStatus: true });
  const where = status
    ? harness.queryBuilder.mergeFilters(base, { status: { eq: status } })
    : base;
  return {
    first: filter.pagingInfo.first,
    after,
    where,
    order: harness.queryBuilder.buildOrderClause(filter),
  };
}

function countsVariables(harness: TestHarness) {
  const base = harness.queryBuilder.buildWhereFilter(harness.filterState.filter(), {
    excludeStatus: true,
  });
  return {
    allWhere: base,
    pendingWhere: harness.queryBuilder.mergeFilters(base, { status: { eq: 'PENDING' } }),
    inProgressWhere: harness.queryBuilder.mergeFilters(base, { status: { eq: 'IN_PROGRESS' } }),
    completedWhere: harness.queryBuilder.mergeFilters(base, { status: { eq: 'COMPLETED' } }),
    expiredWhere: harness.queryBuilder.mergeFilters(base, { status: { eq: 'EXPIRED' } }),
    pendingApprovalWhere: harness.queryBuilder.mergeFilters(base, {
      status: { eq: 'PENDING_APPROVAL' },
    }),
    rejectedWhere: harness.queryBuilder.mergeFilters(base, { status: { eq: 'REJECTED' } }),
  };
}

function seedDetail(harness: TestHarness, refTest: TestRefTest): void {
  harness.cache.writeQuery({
    query: REF_TEST_DETAIL_QUERY,
    variables: { id: refTest.id },
    data: { refTest },
  });
}

function seedList(
  harness: TestHarness,
  status: RefTestStatus | undefined,
  edges: TestEdge[],
  options: {
    totalCount?: number;
    hasNextPage?: boolean;
    endCursor?: string | null;
    after?: string;
  } = {},
): void {
  const endCursor =
    options.endCursor ?? edges[edges.length - 1]?.cursor ?? null;
  harness.cache.writeQuery<TestListData>({
    query: REF_TESTS_QUERY,
    variables: listVariables(harness, status, options.after),
    data: {
      refTests: {
        edges,
        totalCount: options.totalCount ?? edges.length,
        pageInfo: {
          hasNextPage: options.hasNextPage ?? false,
          endCursor,
        },
      },
    },
  });
}

function seedCounts(
  harness: TestHarness,
  counts: Partial<Record<keyof TestCountsData, number>>,
): void {
  const initial = {
    all: 0,
    pending: 0,
    inProgress: 0,
    completed: 0,
    expired: 0,
    pendingApproval: 0,
    rejected: 0,
  };
  const values = { ...initial, ...counts };
  harness.cache.writeQuery<TestCountsData>({
    query: REF_TEST_COUNTS_QUERY,
    variables: countsVariables(harness),
    data: {
      all: { totalCount: values.all },
      pending: { totalCount: values.pending },
      inProgress: { totalCount: values.inProgress },
      completed: { totalCount: values.completed },
      expired: { totalCount: values.expired },
      pendingApproval: { totalCount: values.pendingApproval },
      rejected: { totalCount: values.rejected },
    },
  });
}

function readList(
  harness: TestHarness,
  status?: RefTestStatus,
): TestConnection | null {
  return (
    harness.cache.readQuery<TestListData>({
      query: REF_TESTS_QUERY,
      variables: listVariables(harness, status),
    })?.refTests ?? null
  );
}

function readCounts(harness: TestHarness): TestCountsData | null {
  return harness.cache.readQuery<TestCountsData>({
    query: REF_TEST_COUNTS_QUERY,
    variables: countsVariables(harness),
  });
}

type MutationOperation = {
  variables: Record<string, unknown>;
  update: (cache: ApolloCache, result: { data?: unknown }) => void;
};

/**
 * Lets a test decide when a mutation response arrives. Like Apollo, the response is written to
 * the cache before the mutation's `update` function runs and the result is emitted.
 */
function stubMutation(harness: TestHarness, mutationGql: unknown, document: DocumentNode) {
  const response = new Subject<{ data: unknown; loading: boolean }>();
  let operation: MutationOperation | undefined;
  (mutationGql as { mutate: ReturnType<typeof vi.fn> }).mutate.mockImplementation(
    (options: MutationOperation) => {
      operation = options;
      return response;
    },
  );

  return {
    respond: (data: unknown) => {
      harness.cache.write({
        dataId: 'ROOT_MUTATION',
        query: document,
        variables: operation?.variables,
        result: data,
      });
      operation?.update(harness.cache, { data });
      response.next({ data, loading: false });
      response.complete();
    },
    fail: (error: unknown) => response.error(error),
  };
}

function mutationResponse(
  field: string,
  countField: string,
  rowsField: string,
  rows: readonly Record<string, unknown>[],
) {
  return {
    [field]: {
      __typename: 'MutationPayload',
      [`${field}Result`]: {
        __typename: 'MutationResult',
        totalRequested: rows.length,
        [countField]: rows.length,
        failed: 0,
        [rowsField]: rows.map((row) => ({ __typename: 'RefTest', ...row })),
        errors: [],
      },
    },
  };
}

const RESPONSE_CREATED_AT = '2026-10-05T10:00:00.000Z';

function approvedResponse(id: string) {
  return mutationResponse('approveRefTests', 'successfullyApproved', 'approvedRefTests', [
    { id, status: 'PENDING', createdAt: RESPONSE_CREATED_AT, invitationSent: false },
  ]);
}

function rejectedResponse(id: string) {
  return mutationResponse('rejectRefTests', 'successfullyRejected', 'rejectedRefTests', [
    { id, status: 'REJECTED', rejectionReason: 'Incomplete' },
  ]);
}

function resetResponse(id: string, createdAt = RESPONSE_CREATED_AT) {
  return mutationResponse('resetRefTests', 'successfullyReset', 'resetRefTests', [
    {
      id,
      status: 'PENDING',
      createdAt,
      invitationSent: false,
      resultsSent: false,
      startedAt: null,
      completedAt: null,
      questionScore: null,
      answerScore: null,
      questionTotal: 10,
      answerTotal: null,
      percentage: null,
      selectedAnswerIds: [],
    },
  ]);
}

function revivedResponse(id: string) {
  return mutationResponse('reviveRefTests', 'successfullyRevived', 'revivedRefTests', [
    { id, status: 'PENDING', createdAt: RESPONSE_CREATED_AT, invitationSent: false },
  ]);
}

type TransitionCase = {
  name: string;
  source: RefTestStatus;
  target: RefTestStatus;
  sourceCount: keyof TestCountsData;
  targetCount: keyof TestCountsData;
  event: (id: string) => TestSubscriptionEvent;
  /** Calls the real wrapper and returns a function that delivers its mutation response. */
  start: (harness: TestHarness, id: string) => () => void;
  /** Same as `start` for the detail page's wrapper, which only exists for reset and revive. */
  startFromDetail?: (harness: TestHarness, id: string) => () => void;
};

const APPROVE: TransitionCase = {
  name: 'approve',
  source: 'PENDING_APPROVAL',
  target: 'PENDING',
  sourceCount: 'pendingApproval',
  targetCount: 'pending',
  event: (id) => ({
    __typename: 'RefTestApproved',
    id,
    oldStatus: 'PENDING_APPROVAL',
    status: 'PENDING',
  }),
  start: (harness, id) => {
    const mutation = stubMutation(
      harness,
      TestBed.inject(ApproveRefTestsGQL),
      ApproveRefTestsDocument,
    );
    harness.data.approveRefTests([id], testDestroyRef());
    return () => mutation.respond(approvedResponse(id));
  },
};

const REJECT: TransitionCase = {
  name: 'reject',
  source: 'PENDING_APPROVAL',
  target: 'REJECTED',
  sourceCount: 'pendingApproval',
  targetCount: 'rejected',
  event: (id) => ({
    __typename: 'RefTestRejected',
    id,
    status: 'REJECTED',
    reason: 'Incomplete',
  }),
  start: (harness, id) => {
    const mutation = stubMutation(
      harness,
      TestBed.inject(RejectRefTestsGQL),
      RejectRefTestsDocument,
    );
    harness.data.rejectRefTests([id], 'Incomplete', testDestroyRef());
    return () => mutation.respond(rejectedResponse(id));
  },
};

const RESET: TransitionCase = {
  name: 'reset',
  source: 'COMPLETED',
  target: 'PENDING',
  sourceCount: 'completed',
  targetCount: 'pending',
  event: (id) => ({ __typename: 'RefTestReset', id, oldStatus: 'COMPLETED' }),
  start: (harness, id) => {
    const mutation = stubMutation(
      harness,
      TestBed.inject(ResetRefTestsGQL),
      ResetRefTestsDocument,
    );
    harness.data.resetRefTests(
      { ids: [id], resetType: 'SOFT', regenerateToken: false },
      testDestroyRef(),
    );
    return () => mutation.respond(resetResponse(id));
  },
  startFromDetail: (harness, id) => {
    const mutation = stubMutation(
      harness,
      TestBed.inject(ResetRefTestsGQL),
      ResetRefTestsDocument,
    );
    harness.detailData.setRefTestId(id);
    harness.detailData.resetRefTests(
      { resetType: 'SOFT', regenerateToken: false },
      testDestroyRef(),
    );
    return () => mutation.respond(resetResponse(id));
  },
};

const REVIVE: TransitionCase = {
  name: 'revive',
  source: 'EXPIRED',
  target: 'PENDING',
  sourceCount: 'expired',
  targetCount: 'pending',
  event: (id) => ({ __typename: 'RefTestRevived', id }),
  start: (harness, id) => {
    const mutation = stubMutation(
      harness,
      TestBed.inject(ReviveRefTestsGQL),
      ReviveRefTestsDocument,
    );
    harness.data.reviveRefTests([id], testDestroyRef());
    return () => mutation.respond(revivedResponse(id));
  },
  startFromDetail: (harness, id) => {
    const mutation = stubMutation(
      harness,
      TestBed.inject(ReviveRefTestsGQL),
      ReviveRefTestsDocument,
    );
    harness.detailData.setRefTestId(id);
    harness.detailData.reviveRefTests(testDestroyRef());
    return () => mutation.respond(revivedResponse(id));
  },
};

/**
 * Caches the row only in the "all" window; its source and target status windows are loaded
 * without it, so only the totals can follow the transition.
 */
function seedTransition(
  harness: TestHarness,
  transition: TransitionCase,
  row: TestRefTest,
  options: { counts: boolean } = { counts: true },
): void {
  seedList(harness, undefined, [{ cursor: 'opaque-row', node: row }], { totalCount: 3 });
  seedList(harness, transition.source, [], { totalCount: 2 });
  seedList(harness, transition.target, [], { totalCount: 1 });
  if (options.counts) {
    seedCounts(harness, {
      all: 3,
      [transition.sourceCount]: 2,
      [transition.targetCount]: 1,
    });
  }
}

/** Simulates a refresh that already reflects the transition, as another client's would. */
function refreshAsTransitioned(
  harness: TestHarness,
  transition: TransitionCase,
  id: string,
): void {
  harness.cache.modify({
    id: harness.cache.identify({ __typename: 'RefTest', id }),
    fields: { status: () => transition.target },
  });
  seedList(harness, transition.source, [], { totalCount: 1 });
  seedList(harness, transition.target, [], { totalCount: 2 });
  seedCounts(harness, {
    all: 3,
    [transition.sourceCount]: 1,
    [transition.targetCount]: 2,
  });
}

function expectTransitionedOnce(harness: TestHarness, transition: TransitionCase): void {
  expect(readCounts(harness)).toBeNull();
  expect(readList(harness)).toBeNull();
  expect(readList(harness, transition.source)).toBeNull();
  expect(readList(harness, transition.target)).toBeNull();
  expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
  expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
}

describe('RefTestCacheUpdater subscription reconciliation', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('uses the approved CreatedAt without fabricating a target connection cursor', () => {
    const harness = createHarness();
    const rejected = makeRefTest('rejected', {
      status: 'REJECTED',
      numberOfQuestions: 20,
      rejectionReason: 'Missing details',
    });
    const pending = makeRefTest('pending', {
      status: 'PENDING',
      numberOfQuestions: 10,
    });
    seedList(harness, undefined, [
      { cursor: 'opaque-rejected', node: rejected },
      { cursor: 'opaque-pending', node: pending },
    ]);
    seedList(harness, 'REJECTED', [{ cursor: 'opaque-rejected', node: rejected }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-pending', node: pending }]);
    seedCounts(harness, { all: 2, pending: 1, rejected: 1 });
    seedDetail(harness, rejected);

    const update = vi.spyOn(harness.cacheUpdater, 'updateCacheFromSubscription');
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const event: TestSubscriptionEvent = {
      __typename: 'RefTestApproved',
      id: rejected.id,
      oldStatus: 'REJECTED',
      status: 'PENDING',
      createdAt: RESPONSE_CREATED_AT,
    };
    harness.listEvents.next({ data: { refTestsUpdated: event } });

    expect(update).toHaveBeenCalledWith(event, undefined, 'list');
    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'REJECTED')).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(
      harness.cache.readFragment<{
        status: RefTestStatus;
        createdAt: string;
      }>({
        id: harness.cache.identify({ __typename: 'RefTest', id: rejected.id }),
        fragment: REF_TEST_RESET_FRAGMENT,
      }),
    ).toMatchObject({ status: 'PENDING', createdAt: RESPONSE_CREATED_AT });
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);

    harness.listEvents.next({ data: { refTestsUpdated: event } });

    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.networkRequests()).toBe(0);
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates active lists and counts for an uncached approval transition', () => {
    const harness = createHarness();
    seedList(harness, undefined, [], { totalCount: 4 });
    seedList(harness, 'PENDING', [], { totalCount: 2 });
    seedList(harness, 'REJECTED', [], { totalCount: 1 });
    seedCounts(harness, { all: 4, pending: 2, rejected: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestApproved',
          id: 'unseen-approved',
          oldStatus: 'REJECTED',
          status: 'PENDING',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readList(harness, 'REJECTED')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates counts and lists for cursorless created rows without fabricating edges', () => {
    const harness = createHarness();
    const pending = makeRefTest('pending', {
      status: 'PENDING',
      resultsSent: false,
    });
    seedList(harness, undefined, [{ cursor: 'opaque-pending', node: pending }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-pending', node: pending }]);
    seedCounts(harness, { all: 1, pending: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    const created: TestSubscriptionEvent = {
      __typename: 'RefTestCreated',
      id: 'cursorless-created',
      name: 'New Test',
      email: 'new@example.test',
      titleId: null,
      titleValue: null,
      invitationSent: false,
      resultsSent: false,
      sendInvitationsAutomatically: false,
      sendResultsAutomatically: false,
      status: 'PENDING',
      numberOfQuestions: 10,
      maxTimeInMinutes: 30,
      firstName: 'Matching',
      lastName: 'Test',
      createdAt: '2026-10-05T10:00:00.000Z',
      scheduledAt: null,
    };
    harness.listEvents.next({ data: { refTestsUpdated: created } });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates filtered lists and counts for an uncached invitation match', () => {
    const invited = makeRefTest('invitation-match', {
      status: 'PENDING',
      invitationSent: false,
    });
    const harness = createHarness({ invitationSent: true });
    harness.cache.writeQuery({
      query: REF_TEST_DETAIL_QUERY,
      variables: { id: invited.id },
      data: { refTest: invited },
    });
    seedList(harness, undefined, [], { totalCount: 0 });
    seedList(harness, 'PENDING', [], { totalCount: 0 });
    seedCounts(harness, { all: 0, pending: 0 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestInvitationSent',
          id: invited.id,
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: invited.id });
    expect(
      harness.cache.readFragment<{ invitationSent: boolean }>({
        id: entityId,
        fragment: REF_TEST_RESET_FRAGMENT,
      })?.invitationSent,
    ).toBe(true);
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates status lists and counts for unseen transitions without inserting rows', () => {
    const harness = createHarness();
    const pending = makeRefTest('pending', { status: 'PENDING' });
    const inProgress = makeRefTest('in-progress', { status: 'IN_PROGRESS' });
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-pending', node: pending },
        { cursor: 'opaque-in-progress', node: inProgress },
      ],
      { totalCount: 5, hasNextPage: true },
    );
    seedList(harness, 'PENDING', [{ cursor: 'opaque-pending', node: pending }], {
      totalCount: 2,
      hasNextPage: true,
    });
    seedList(harness, 'IN_PROGRESS', [{ cursor: 'opaque-in-progress', node: inProgress }], {
      totalCount: 1,
    });
    seedCounts(harness, { all: 5, pending: 2, inProgress: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    const event: TestSubscriptionEvent = {
      __typename: 'RefTestStarted',
      id: 'unseen-pending',
      status: 'IN_PROGRESS',
      startedAt: '2026-10-01T11:00:00.000Z',
    };
    harness.listEvents.next({ data: { refTestsUpdated: event } });
    harness.listEvents.next({ data: { refTestsUpdated: event } });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readList(harness, 'IN_PROGRESS')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates lists and counts after a reset changes status and cursor ordering', () => {
    const harness = createHarness({ sortField: 'numberOfQuestions' });
    const resetTest = makeRefTest('reset', {
      status: 'COMPLETED',
      invitationSent: true,
      resultsSent: true,
      questionScore: 8,
      answerScore: 8,
      questionTotal: 10,
      answerTotal: 8,
      percentage: 80,
      language: 'en',
      startedAt: '2026-10-01T10:00:00.000Z',
      completedAt: '2026-10-01T10:20:00.000Z',
    });
    const other = makeRefTest('other', {
      status: 'PENDING',
      createdAt: '2026-10-02T10:00:00.000Z',
    });
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-reset', node: resetTest },
        { cursor: 'opaque-other', node: other },
      ],
      { totalCount: 2, hasNextPage: true },
    );
    seedList(harness, 'COMPLETED', [{ cursor: 'opaque-reset', node: resetTest }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-other', node: other }]);
    seedCounts(harness, { all: 2, completed: 1, pending: 1 });
    seedDetail(harness, resetTest);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestReset',
          id: resetTest.id,
          oldStatus: 'COMPLETED',
          status: 'PENDING',
          resetType: 'HARD',
          createdAt: '2026-10-03T10:00:00.000Z',
          invitationSent: false,
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'COMPLETED')).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: resetTest.id });
    expect(
      harness.cache.readFragment<{
        status: RefTestStatus;
        invitationSent: boolean;
        createdAt: string;
        resultsSent: boolean;
        questionTotal: number;
        answerTotal: number | null;
        percentage: number | null;
        language: string | null;
      }>({
        id: entityId,
        fragment: REF_TEST_RESET_FRAGMENT,
      }),
    ).toMatchObject({
      status: 'PENDING',
      invitationSent: false,
      createdAt: '2026-10-03T10:00:00.000Z',
      resultsSent: false,
      questionTotal: 10,
      answerTotal: null,
      percentage: null,
      language: null,
    });
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates lists and counts after a revive replaces its timestamp', () => {
    const harness = createHarness({ sortField: 'numberOfQuestions' });
    const revived = makeRefTest('revived', {
      status: 'EXPIRED',
      invitationSent: true,
      resultsSent: true,
      language: 'fr',
      createdAt: '2026-01-01T10:00:00.000Z',
    });
    const other = makeRefTest('other', {
      status: 'PENDING',
      createdAt: '2026-10-02T10:00:00.000Z',
    });
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-other', node: other },
        { cursor: 'opaque-revived', node: revived },
      ],
      { totalCount: 2, hasNextPage: true },
    );
    seedList(harness, 'EXPIRED', [{ cursor: 'opaque-revived', node: revived }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-other', node: other }]);
    seedCounts(harness, { all: 2, expired: 1, pending: 1 });
    seedDetail(harness, revived);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestRevived',
          id: revived.id,
          status: 'PENDING',
          createdAt: RESPONSE_CREATED_AT,
          invitationSent: false,
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'EXPIRED')).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: revived.id });
    expect(
      harness.cache.readFragment<{
        status: RefTestStatus;
        invitationSent: boolean;
        resultsSent: boolean;
        language: string | null;
        createdAt: string;
      }>({
        id: entityId,
        fragment: REF_TEST_RESET_FRAGMENT,
      }),
    ).toMatchObject({
      status: 'PENDING',
      invitationSent: false,
      resultsSent: true,
      language: 'fr',
      createdAt: RESPONSE_CREATED_AT,
    });
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates invitation-filtered connections when reset membership changes', () => {
    const harness = createHarness({
      invitationSent: true,
      sortField: 'numberOfQuestions',
    });
    const resetTest = makeRefTest('reset-filtered', {
      status: 'COMPLETED',
      invitationSent: true,
      resultsSent: true,
    });
    seedList(harness, undefined, [{ cursor: 'opaque-reset-filtered', node: resetTest }], {
      totalCount: 1,
    });
    seedList(harness, 'COMPLETED', [
      { cursor: 'opaque-reset-filtered', node: resetTest },
    ]);
    seedCounts(harness, { all: 1, completed: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestReset',
          id: resetTest.id,
          oldStatus: 'COMPLETED',
          status: 'PENDING',
          resetType: 'SOFT',
          createdAt: resetTest.createdAt,
          invitationSent: false,
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'COMPLETED')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('replays early subscription events and refreshes after the initial list query loads', async () => {
    const harness = createHarness({ resultsSent: false });
    const pending = makeRefTest('early-event', {
      status: 'PENDING',
      resultsSent: false,
    });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    await vi.waitFor(() => expect(harness.listQueryRef.setVariables).toHaveBeenCalled());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestResultSent',
          id: pending.id,
        },
      },
    });
    expect(readList(harness)).toBeNull();

    seedList(harness, undefined, [{ cursor: 'opaque-early', node: pending }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-early', node: pending }]);
    seedCounts(harness, { all: 1, pending: 1 });
    const connection = readList(harness);
    if (!connection) throw new Error('Expected the initial list response to be cached');
    harness.listQueryResults.next({ data: { refTests: connection } });

    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.networkRequests()).toBe(0);
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('waits for the active list then refreshes when a cached entity changes membership', async () => {
    const harness = createHarness({ resultsSent: false });
    const pending = makeRefTest('early-cached-entity', {
      status: 'PENDING',
      resultsSent: false,
    });
    harness.cache.writeFragment({
      id: harness.cache.identify({ __typename: 'RefTest', id: pending.id }),
      fragment: REF_TEST_REPLAY_BEFORE_FRAGMENT,
      data: {
        __typename: 'RefTest',
        id: pending.id,
        status: pending.status,
        resultsSent: pending.resultsSent,
      },
    });
    seedCounts(harness, { all: 1, pending: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    await vi.waitFor(() => expect(harness.listQueryRef.setVariables).toHaveBeenCalled());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestResultSent',
          id: pending.id,
        },
      },
    });
    expect(
      harness.cache.readFragment<{ resultsSent: boolean }>({
        id: harness.cache.identify({ __typename: 'RefTest', id: pending.id }),
        fragment: REF_TEST_STATE_FRAGMENT,
      })?.resultsSent,
    ).toBe(false);

    seedList(harness, undefined, [{ cursor: 'opaque-cached-early', node: pending }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-cached-early', node: pending }]);
    const connection = readList(harness);
    if (!connection) throw new Error('Expected the initial list response to be cached');
    harness.listQueryResults.next({ data: { refTests: connection } });

    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.networkRequests()).toBe(0);
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('waits for the initial counter query before invalidating changed membership', () => {
    const harness = createHarness({ resultsSent: false });
    const pending = makeRefTest('early-counts', {
      status: 'PENDING',
      resultsSent: false,
    });
    seedList(harness, undefined, [{ cursor: 'opaque-early-counts', node: pending }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-early-counts', node: pending }]);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestResultSent',
          id: pending.id,
        },
      },
    });
    expect(readList(harness)?.edges).toHaveLength(1);

    seedCounts(harness, { all: 1, pending: 1 });
    harness.cacheUpdater.replayPendingEvents();

    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.networkRequests()).toBe(0);
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('refreshes authoritative list and count data after in-flight responses race a deletion', async () => {
    const harness = createHarness();
    const deleted = makeRefTest('late-delete', { status: 'PENDING' });
    const survivor = makeRefTest('late-survivor', { status: 'PENDING' });
    const staleConnection: TestConnection = {
      edges: [
        { cursor: 'stale-deleted', node: deleted },
        { cursor: 'stale-survivor', node: survivor },
      ],
      totalCount: 2,
      pageInfo: { hasNextPage: false, endCursor: 'stale-survivor' },
    };
    const authoritativeConnection: TestConnection = {
      edges: [{ cursor: 'server-survivor', node: survivor }],
      totalCount: 1,
      pageInfo: { hasNextPage: false, endCursor: 'server-survivor' },
    };
    seedList(harness, undefined, staleConnection.edges, { totalCount: 2 });
    seedList(harness, 'PENDING', staleConnection.edges, { totalCount: 2 });
    seedCounts(harness, { all: 2, pending: 2 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    await vi.waitFor(() => {
      expect(harness.listQueryRef.setVariables).toHaveBeenCalled();
      expect(harness.countsQueryRef.setVariables).toHaveBeenCalled();
    });

    harness.listQueryRef.refetch.mockImplementation(() => {
      seedList(harness, undefined, authoritativeConnection.edges, {
        totalCount: authoritativeConnection.totalCount,
        endCursor: authoritativeConnection.pageInfo.endCursor,
      });
      harness.listQueryResults.next({ data: { refTests: authoritativeConnection }, loading: false });
      return Promise.resolve({ data: { refTests: authoritativeConnection } });
    });
    harness.countsQueryRef.refetch.mockImplementation(() => {
      const authoritativeCounts: TestCountsData = {
        all: { totalCount: 1 },
        pending: { totalCount: 1 },
        inProgress: { totalCount: 0 },
        completed: { totalCount: 0 },
        expired: { totalCount: 0 },
        pendingApproval: { totalCount: 0 },
        rejected: { totalCount: 0 },
      };
      seedCounts(harness, { all: 1, pending: 1 });
      harness.countsQueryResults.next({ data: authoritativeCounts, loading: false });
      return Promise.resolve({ data: authoritativeCounts });
    });

    harness.listQueryResults.next({ data: { refTests: staleConnection }, loading: true });
    harness.countsQueryResults.next({
      data: {
        all: { totalCount: 2 },
        pending: { totalCount: 2 },
        inProgress: { totalCount: 0 },
        completed: { totalCount: 0 },
        expired: { totalCount: 0 },
        pendingApproval: { totalCount: 0 },
        rejected: { totalCount: 0 },
      },
      loading: true,
    });
    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestDeleted',
          id: deleted.id,
          status: 'PENDING',
        },
      },
    });

    expect(readList(harness)?.edges.map((edge) => edge.node.id)).toEqual([
      deleted.id,
      survivor.id,
    ]);

    // Apollo writes each stale result before valueChanges emits it. The event is held until
    // both reads finish, then invalidation forces the active queries to fetch current data.
    seedList(harness, undefined, staleConnection.edges, { totalCount: 2 });
    harness.listQueryResults.next({ data: { refTests: staleConnection }, loading: false });
    seedCounts(harness, { all: 2, pending: 2 });
    harness.countsQueryResults.next({
      data: {
        all: { totalCount: 2 },
        pending: { totalCount: 2 },
        inProgress: { totalCount: 0 },
        completed: { totalCount: 0 },
        expired: { totalCount: 0 },
        pendingApproval: { totalCount: 0 },
        rejected: { totalCount: 0 },
      },
      loading: false,
    });

    await vi.waitFor(() => {
      expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
      expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
      expect(readList(harness)?.edges.map((edge) => edge.node.id)).toEqual([survivor.id]);
      expect(readCounts(harness)).toMatchObject({
        all: { totalCount: 1 },
        pending: { totalCount: 1 },
      });
    });
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(
      harness.cache.readFragment({
        id: harness.cache.identify({ __typename: 'RefTest', id: deleted.id }),
        fragment: REF_TEST_STATE_FRAGMENT,
      }),
    ).toBeNull();
  });

  it('invalidates lists and counts when an in-flight list query already includes a queued transition', async () => {
    const before = makeRefTest('list-query-transition', {
      status: 'PENDING_APPROVAL',
      createdAt: '2026-10-03T10:00:00.000Z',
    });
    const neighbor = makeRefTest('list-query-neighbor', {
      status: 'PENDING',
      createdAt: '2026-10-02T10:00:00.000Z',
    });
    const updated = makeRefTest(before.id, {
      status: 'PENDING',
      createdAt: '2026-10-01T10:00:00.000Z',
    });
    const harness = createHarness();
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-list-query-before', node: before },
        { cursor: 'opaque-list-query-neighbor', node: neighbor },
      ],
      { totalCount: 2, hasNextPage: true, endCursor: 'opaque-list-query-neighbor' },
    );
    seedList(harness, 'PENDING', [{ cursor: 'opaque-pending-neighbor', node: neighbor }]);
    seedCounts(harness, { all: 2, pending: 1, pendingApproval: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    await vi.waitFor(() => expect(harness.listQueryRef.setVariables).toHaveBeenCalled());

    const staleConnection: TestConnection = {
      edges: [{ cursor: 'opaque-list-query-before', node: before }],
      totalCount: 2,
      pageInfo: { hasNextPage: true, endCursor: 'opaque-list-query-before' },
    };
    harness.listQueryResults.next({ data: { refTests: staleConnection }, loading: true });
    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestApproved',
          id: before.id,
          oldStatus: 'PENDING_APPROVAL',
          status: 'PENDING',
          createdAt: updated.createdAt,
        },
      },
    });

    const postEventConnection: TestConnection = {
      edges: [
        { cursor: 'server-updated-row', node: updated },
        { cursor: 'server-neighbor', node: neighbor },
      ],
      totalCount: 2,
      pageInfo: { hasNextPage: true, endCursor: 'server-neighbor' },
    };
    seedList(harness, undefined, postEventConnection.edges, {
      totalCount: 2,
      hasNextPage: true,
      endCursor: postEventConnection.pageInfo.endCursor,
    });
    harness.listQueryResults.next({ data: { refTests: postEventConnection }, loading: false });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('waits for an in-flight fetchMore before invalidating shifted connection cursors', async () => {
    const harness = createHarness({ pagingInfo: { first: 2 } });
    const deleted = makeRefTest('fetch-more-delete', { status: 'PENDING' });
    const survivor = makeRefTest('fetch-more-survivor', { status: 'PENDING' });
    const third = makeRefTest('fetch-more-third', { status: 'PENDING' });
    const firstPage: TestConnection = {
      edges: [
        { cursor: 'first-cursor', node: deleted },
        { cursor: 'second-cursor', node: survivor },
      ],
      totalCount: 3,
      pageInfo: { hasNextPage: true, endCursor: 'second-cursor' },
    };
    const authoritativeConnection: TestConnection = {
      edges: [
        { cursor: 'server-survivor', node: survivor },
        { cursor: 'server-third', node: third },
      ],
      totalCount: 2,
      pageInfo: { hasNextPage: true, endCursor: 'server-third' },
    };
    seedList(harness, undefined, firstPage.edges, {
      totalCount: 3,
      hasNextPage: true,
      endCursor: 'second-cursor',
    });
    seedCounts(harness, { all: 3, pending: 3 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    await vi.waitFor(() => expect(harness.listQueryRef.setVariables).toHaveBeenCalled());
    harness.listQueryResults.next({ data: { refTests: firstPage }, loading: false });

    const authoritativeCounts: TestCountsData = {
      all: { totalCount: 2 },
      pending: { totalCount: 2 },
      inProgress: { totalCount: 0 },
      completed: { totalCount: 0 },
      expired: { totalCount: 0 },
      pendingApproval: { totalCount: 0 },
      rejected: { totalCount: 0 },
    };
    harness.listQueryRef.refetch.mockImplementation(() => {
      seedList(harness, undefined, authoritativeConnection.edges, {
        totalCount: authoritativeConnection.totalCount,
        hasNextPage: authoritativeConnection.pageInfo.hasNextPage,
        endCursor: authoritativeConnection.pageInfo.endCursor,
      });
      harness.listQueryResults.next({ data: { refTests: authoritativeConnection }, loading: false });
      return Promise.resolve({ data: { refTests: authoritativeConnection } });
    });
    harness.countsQueryRef.refetch.mockImplementation(() => {
      seedCounts(harness, { all: 2, pending: 2 });
      harness.countsQueryResults.next({ data: authoritativeCounts, loading: false });
      return Promise.resolve({ data: authoritativeCounts });
    });

    let finishFetchMore!: (value: { data: unknown }) => void;
    const inFlightFetchMore = new Promise<{ data: unknown }>((resolve) => {
      finishFetchMore = resolve;
    });
    harness.listQueryRef.fetchMore.mockReturnValue(inFlightFetchMore);
    await vi.waitFor(() =>
      expect(harness.data.queryResult()?.data?.refTests?.pageInfo?.endCursor).toBe('second-cursor'),
    );
    const fetchMore = harness.data.fetchMore().subscribe();
    expect(harness.listQueryRef.fetchMore).toHaveBeenCalledWith({
      variables: expect.objectContaining({ after: 'second-cursor' }),
    });

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestDeleted',
          id: deleted.id,
          status: 'PENDING',
        },
      },
    });
    expect(readList(harness)?.edges.map((edge) => edge.node.id)).toEqual([
      deleted.id,
      survivor.id,
    ]);
    fetchMore.unsubscribe();
    expect(harness.listQueryRef.refetch).not.toHaveBeenCalled();

    seedList(
      harness,
      undefined,
      [{ cursor: 'stale-third', node: third }],
      {
        totalCount: 3,
        hasNextPage: false,
        endCursor: 'stale-third',
        after: 'second-cursor',
      },
    );
    const staleMergedConnection = readList(harness);
    if (!staleMergedConnection) throw new Error('Expected the fetchMore response in the cache'    );
    harness.listQueryResults.next({ data: { refTests: staleMergedConnection }, loading: false });
    finishFetchMore({ data: { refTests: staleMergedConnection } });

    await vi.waitFor(() => {
      expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
      expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
      expect(readList(harness)?.edges.map((edge) => edge.node.id)).toEqual([
        survivor.id,
        third.id,
      ]);
    });

    harness.listQueryRef.fetchMore.mockResolvedValue({ data: {} });
    await firstValueFrom(harness.data.fetchMore());
    expect(harness.listQueryRef.fetchMore).toHaveBeenLastCalledWith({
      variables: expect.objectContaining({ after: 'server-third' }),
    });
    expect(readCounts(harness)).toMatchObject({
      all: { totalCount: 2 },
      pending: { totalCount: 2 },
    });
  });

  it('reconciles events received during an in-flight refetch before exposing its stale cache', async () => {
    const harness = createHarness();
    const deleted = makeRefTest('refetch-delete', { status: 'PENDING' });
    const survivor = makeRefTest('refetch-survivor', { status: 'PENDING' });
    const staleConnection: TestConnection = {
      edges: [
        { cursor: 'stale-delete', node: deleted },
        { cursor: 'stale-survivor', node: survivor },
      ],
      totalCount: 2,
      pageInfo: { hasNextPage: false, endCursor: 'stale-survivor' },
    };
    const authoritativeConnection: TestConnection = {
      edges: [{ cursor: 'server-survivor', node: survivor }],
      totalCount: 1,
      pageInfo: { hasNextPage: false, endCursor: 'server-survivor' },
    };
    seedList(harness, undefined, staleConnection.edges, { totalCount: 2 });
    seedCounts(harness, { all: 2, pending: 2 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    let finishOldList!: (value: { data: unknown }) => void;
    let finishOldCounts!: (value: { data: unknown }) => void;
    const oldListRefetch = new Promise<{ data: unknown }>((resolve) => {
      finishOldList = resolve;
    });
    const oldCountsRefetch = new Promise<{ data: unknown }>((resolve) => {
      finishOldCounts = resolve;
    });
    let listRefetches = 0;
    let countsRefetches = 0;
    harness.listQueryRef.refetch.mockImplementation(() => {
      listRefetches++;
      if (listRefetches === 1) return oldListRefetch;
      seedList(harness, undefined, authoritativeConnection.edges, {
        totalCount: authoritativeConnection.totalCount,
        endCursor: authoritativeConnection.pageInfo.endCursor,
      });
      harness.listQueryResults.next({ data: { refTests: authoritativeConnection }, loading: false });
      return Promise.resolve({ data: { refTests: authoritativeConnection } });
    });
    harness.countsQueryRef.refetch.mockImplementation(() => {
      countsRefetches++;
      if (countsRefetches === 1) return oldCountsRefetch;
      const authoritativeCounts: TestCountsData = {
        all: { totalCount: 1 },
        pending: { totalCount: 1 },
        inProgress: { totalCount: 0 },
        completed: { totalCount: 0 },
        expired: { totalCount: 0 },
        pendingApproval: { totalCount: 0 },
        rejected: { totalCount: 0 },
      };
      seedCounts(harness, { all: 1, pending: 1 });
      harness.countsQueryResults.next({ data: authoritativeCounts, loading: false });
      return Promise.resolve({ data: authoritativeCounts });
    });

    harness.data.reset();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestDeleted',
          id: deleted.id,
          status: 'PENDING',
        },
      },
    });

    seedList(harness, undefined, staleConnection.edges, { totalCount: 2 });
    harness.listQueryResults.next({ data: { refTests: staleConnection }, loading: false });
    seedCounts(harness, { all: 2, pending: 2 });
    const staleCounts: TestCountsData = {
      all: { totalCount: 2 },
      pending: { totalCount: 2 },
      inProgress: { totalCount: 0 },
      completed: { totalCount: 0 },
      expired: { totalCount: 0 },
      pendingApproval: { totalCount: 0 },
      rejected: { totalCount: 0 },
    };
    harness.countsQueryResults.next({ data: staleCounts, loading: false });
    finishOldList({ data: { refTests: staleConnection } });
    finishOldCounts({ data: staleCounts });

    await vi.waitFor(() => {
      expect(listRefetches).toBe(2);
      expect(countsRefetches).toBe(2);
      expect(readList(harness)?.edges.map((edge) => edge.node.id)).toEqual([survivor.id]);
      expect(readCounts(harness)).toMatchObject({
        all: { totalCount: 1 },
        pending: { totalCount: 1 },
      });
    });
  });

  it('replays detail events after the opened RefTest query first populates the cache', async () => {
    const detailTest = makeRefTest('early-detail', {
      status: 'PENDING',
      resultsSent: false,
    });
    const harness = createHarness({}, detailTest);
    harness.detailQueryResults.next({ data: {}, loading: true });
    harness.detailData.setRefTestId(detailTest.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    await vi.waitFor(() => expect(harness.detailSubscribe).toHaveBeenCalled());

    const event: TestSubscriptionEvent = {
      __typename: 'RefTestResultSent',
      id: detailTest.id,
    };
    harness.detailEvents.next({ data: { refTestUpdated: event } });

    harness.cache.writeQuery({
      query: REF_TEST_DETAIL_QUERY,
      variables: { id: detailTest.id },
      data: { refTest: detailTest },
    });
    harness.detailQueryResults.next({ data: { refTest: detailTest }, loading: false });

    const entityId = harness.cache.identify({ __typename: 'RefTest', id: detailTest.id });
    expect(
      harness.cache.readFragment<{ resultsSent: boolean }>({
        id: entityId,
        fragment: REF_TEST_STATE_FRAGMENT,
      })?.resultsSent,
    ).toBe(true);
    expect(harness.detailQueryRef.refetch).not.toHaveBeenCalled();
    expect(harness.networkRequests()).toBe(0);
    subscription.unsubscribe();
  });

  it('redacts anonymized participant fields and invalidates search-filtered connections', () => {
    const harness = createHarness({ searchTerm: 'Firstanonymous' });
    const refTest = makeRefTest('anonymous', {
      status: 'PENDING',
      rejectionReason: 'Sensitive reason',
    });
    seedList(harness, undefined, [{ cursor: 'opaque-anonymous', node: refTest }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-anonymous', node: refTest }]);
    seedCounts(harness, { all: 1, pending: 1 });
    seedDetail(harness, refTest);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestAnonymized',
          id: refTest.id,
          status: 'PENDING',
          name: '*** ***',
          email: '***',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: refTest.id });
    expect(
      harness.cache.readFragment<{
        firstName: string;
        lastName: string;
        name: string;
        email: string;
        isAnonymized: boolean;
        rejectionReason: string | null;
      }>({
        id: entityId,
        fragment: REF_TEST_ANONYMIZED_FRAGMENT,
      }),
    ).toMatchObject({
      firstName: '***',
      lastName: '***',
      name: '*** ***',
      email: '***',
      isAnonymized: true,
      rejectionReason: null,
    });
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
  });

  it.each([
    { id: 'case-insensitive-search', searchTerm: 'ALICE', firstName: 'Alice' },
    { id: 'accent-insensitive-search', searchTerm: 'eclair', firstName: 'Éclair' },
  ])(
    'preserves loaded search membership for $id when the search fields are unchanged',
    ({ id, searchTerm, firstName }) => {
      const harness = createHarness({
        searchTerm,
        sortField: 'numberOfQuestions',
        sortDirection: 'DESC',
      });
      const refTest = makeRefTest(id, {
        status: 'COMPLETED',
        firstName,
        lastName: 'Referee',
        name: `${firstName} Referee`,
        email: `${id}@example.test`,
      });
      seedList(harness, undefined, [{ cursor: `opaque-${id}`, node: refTest }]);
      seedList(harness, 'COMPLETED', [{ cursor: `opaque-${id}`, node: refTest }]);
      seedCounts(harness, { all: 1, completed: 1 });
      harness.data.subscribeToRefTestUpdates(testDestroyRef());

      harness.listEvents.next({
        data: {
          refTestsUpdated: {
            __typename: 'RefTestCompleted',
            id,
            status: 'COMPLETED',
            questionTotal: 11,
            answerTotal: 10,
          },
        },
      });

      expect(readList(harness)?.edges[0].node.status).toBe('COMPLETED');
      expect(readList(harness)?.edges[0].node.questionTotal).toBe(11);
      expect(readCounts(harness)).toMatchObject({
        all: { totalCount: 1 },
        completed: { totalCount: 1 },
      });
      expect(harness.networkRequests()).toBe(0);
      expect(harness.listQueryRef.refetch).not.toHaveBeenCalled();
      expect(harness.countsQueryRef.refetch).not.toHaveBeenCalled();
    },
  );

  it('invalidates lists and counts when updated sort placement is unknowable', () => {
    const harness = createHarness({
      sortField: 'completedAt',
      sortDirection: 'DESC',
    });
    const inProgress = makeRefTest('in-progress', {
      status: 'IN_PROGRESS',
      completedAt: null,
    });
    const pending = makeRefTest('pending', {
      status: 'PENDING',
      completedAt: null,
    });
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-in-progress', node: inProgress },
        { cursor: 'opaque-pending', node: pending },
      ],
      { totalCount: 2, hasNextPage: true },
    );
    seedList(harness, 'IN_PROGRESS', [{ cursor: 'opaque-in-progress', node: inProgress }], {
      totalCount: 1,
    });
    seedList(harness, 'PENDING', [{ cursor: 'opaque-pending', node: pending }], {
      totalCount: 1,
    });
    seedList(harness, 'COMPLETED', [], { totalCount: 0 });
    seedCounts(harness, { all: 2, pending: 1, inProgress: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestCompleted',
          id: inProgress.id,
          status: 'COMPLETED',
          completedAt: '2026-10-01T12:00:00.000Z',
          questionScore: 80,
          questionTotal: 10,
          answerScore: 80,
          answerTotal: 10,
          percentage: 80,
          language: 'en',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'COMPLETED')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates opaque cursors when an update changes a connection order', () => {
    const harness = createHarness({
      sortField: 'questionScore',
      sortDirection: 'DESC',
      pagingInfo: { first: 2 },
    });
    const first = makeRefTest('first', {
      status: 'COMPLETED',
      questionScore: 50,
      completedAt: '2026-10-01T10:00:00.000Z',
    });
    const second = makeRefTest('second', {
      status: 'COMPLETED',
      questionScore: 30,
      completedAt: '2026-10-01T09:00:00.000Z',
    });
    const third = makeRefTest('third', {
      status: 'COMPLETED',
      questionScore: 10,
      completedAt: '2026-10-01T08:00:00.000Z',
    });
    const firstPage = [
      { cursor: 'opaque-first', node: first },
      { cursor: 'opaque-second', node: second },
    ];
    seedList(harness, undefined, firstPage, {
      totalCount: 4,
      hasNextPage: true,
      endCursor: 'opaque-second',
    });
    seedList(harness, undefined, [{ cursor: 'opaque-third', node: third }], {
      totalCount: 4,
      hasNextPage: true,
      after: 'opaque-second',
    });
    seedList(harness, 'COMPLETED', [
      ...firstPage,
      { cursor: 'opaque-third', node: third },
    ], {
      totalCount: 4,
      hasNextPage: true,
      endCursor: 'opaque-third',
    });
    seedCounts(harness, { all: 4, completed: 4 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestCompleted',
          id: third.id,
          status: 'COMPLETED',
          completedAt: third.completedAt ?? '2026-10-01T08:00:00.000Z',
          questionScore: 60,
          questionTotal: 10,
          answerScore: 8,
          answerTotal: 10,
          percentage: 80,
          language: 'en',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'COMPLETED')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
  });

  it('invalidates a connection when provider-dependent text sort placement is unknown', () => {
    const harness = createHarness({
      sortField: 'email',
      sortDirection: 'ASC',
    });
    const anonymized = makeRefTest('string-sort', {
      firstName: 'Alice',
      lastName: 'Anders',
      name: 'Alice Anders',
      email: 'alice@example.test',
    });
    const other = makeRefTest('string-sort-other', {
      firstName: 'Bob',
      lastName: 'Baker',
      name: 'Bob Baker',
      email: 'bob@example.test',
    });
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-string-sort', node: anonymized },
        { cursor: 'opaque-string-sort-other', node: other },
      ],
      { totalCount: 2, hasNextPage: true },
    );
    seedList(harness, 'PENDING', [
      { cursor: 'opaque-string-sort', node: anonymized },
      { cursor: 'opaque-string-sort-other', node: other },
    ]);
    seedCounts(harness, { all: 2, pending: 2 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestAnonymized',
          id: anonymized.id,
          status: 'PENDING',
          name: '*** ***',
          email: '***',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
  });

  it('invalidates shifted connection windows after deletion rather than retaining a cursor', () => {
    const harness = createHarness();
    const deleted = makeRefTest('last-loaded', { status: 'PENDING' });
    seedList(
      harness,
      undefined,
      [{ cursor: 'opaque-last-loaded', node: deleted }],
      { totalCount: 3, hasNextPage: true, endCursor: 'opaque-last-loaded' },
    );
    seedList(
      harness,
      'PENDING',
      [{ cursor: 'opaque-last-loaded', node: deleted }],
      { totalCount: 3, hasNextPage: true, endCursor: 'opaque-last-loaded' },
    );
    seedCounts(harness, { all: 3, pending: 3 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestDeleted',
          id: deleted.id,
          status: 'PENDING',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
  });

  it('routes detail subscription updates through the shared cache updater and refreshes affected queries', async () => {
    const detailTest = makeRefTest('detail', {
      status: 'PENDING',
      resultsSent: false,
    });
    const harness = createHarness({ resultsSent: false }, detailTest);
    seedList(harness, undefined, [{ cursor: 'opaque-detail', node: detailTest }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-detail', node: detailTest }]);
    seedCounts(harness, { all: 1, pending: 1 });
    seedDetail(harness, detailTest);

    const update = vi.spyOn(harness.cacheUpdater, 'updateCacheFromSubscription');
    harness.detailData.setRefTestId(detailTest.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    await vi.waitFor(() => {
      expect(harness.detailWatch).toHaveBeenCalled();
      expect(harness.detailSubscribe).toHaveBeenCalled();
    });

    const event: TestSubscriptionEvent = {
      __typename: 'RefTestResultSent',
      id: detailTest.id,
    };
    harness.detailEvents.next({ data: { refTestUpdated: event } });

    expect(update).toHaveBeenCalledWith(
      event,
      expect.objectContaining({ id: detailTest.id }),
      'detail',
    );
    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: detailTest.id });
    expect(
      harness.cache.readFragment<{ resultsSent: boolean }>({
        id: entityId,
        fragment: REF_TEST_STATE_FRAGMENT,
      })?.resultsSent,
    ).toBe(true);
    expect(harness.detailQueryRef.refetch).not.toHaveBeenCalled();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
    subscription.unsubscribe();
  });

  it('does not double-count a detail event when the list event already updated the cache', async () => {
    const updated = makeRefTest('cross-path', {
      status: 'PENDING',
      resultsSent: false,
    });
    const other = makeRefTest('cross-path-other', {
      status: 'PENDING',
      resultsSent: false,
    });
    const harness = createHarness({ resultsSent: false }, updated);
    seedList(harness, undefined, [
      { cursor: 'opaque-cross-path', node: updated },
      { cursor: 'opaque-cross-path-other', node: other },
    ]);
    seedList(harness, 'PENDING', [
      { cursor: 'opaque-cross-path', node: updated },
      { cursor: 'opaque-cross-path-other', node: other },
    ]);
    seedCounts(harness, { all: 2, pending: 2 });
    seedDetail(harness, updated);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const event: TestSubscriptionEvent = {
      __typename: 'RefTestResultSent',
      id: updated.id,
    };
    harness.listEvents.next({ data: { refTestsUpdated: event } });
    expect(readCounts(harness)).toBeNull();

    harness.detailData.setRefTestId(updated.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    await vi.waitFor(() => expect(harness.detailSubscribe).toHaveBeenCalled());
    harness.detailEvents.next({ data: { refTestUpdated: event } });

    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
    subscription.unsubscribe();
  });

  it('replays a detail event received before its initial result despite unrelated list data', async () => {
    const detailTest = makeRefTest('detail-early', {
      status: 'PENDING',
      resultsSent: false,
    });
    const other = makeRefTest('unrelated-list-row', {
      status: 'PENDING',
      resultsSent: false,
    });
    const harness = createHarness({ resultsSent: false }, detailTest);
    seedList(
      harness,
      undefined,
      [{ cursor: 'opaque-unrelated', node: other }],
      { totalCount: 2, hasNextPage: true },
    );
    seedList(
      harness,
      'PENDING',
      [{ cursor: 'opaque-unrelated', node: other }],
      { totalCount: 2, hasNextPage: true },
    );
    seedCounts(harness, { all: 2, pending: 2 });
    harness.detailQueryResults.next({ data: {}, loading: true });
    harness.detailData.setRefTestId(detailTest.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    await vi.waitFor(() => {
      expect(harness.detailWatch).toHaveBeenCalled();
      expect(harness.detailSubscribe).toHaveBeenCalled();
    });

    harness.detailEvents.next({
      data: {
        refTestUpdated: {
          __typename: 'RefTestResultSent',
          id: detailTest.id,
        },
      },
    });
    expect(
      harness.cache.readFragment<{ resultsSent: boolean }>({
        id: harness.cache.identify({ __typename: 'RefTest', id: detailTest.id }),
        fragment: REF_TEST_STATE_FRAGMENT,
      }),
    ).toBeNull();

    harness.cache.writeQuery({
      query: REF_TEST_DETAIL_QUERY,
      variables: { id: detailTest.id },
      data: { refTest: detailTest },
    });
    harness.detailQueryResults.next({ data: { refTest: detailTest }, loading: false });

    const entityId = harness.cache.identify({ __typename: 'RefTest', id: detailTest.id });
    expect(
      harness.cache.readFragment<{ resultsSent: boolean }>({
        id: entityId,
        fragment: REF_TEST_STATE_FRAGMENT,
      })?.resultsSent,
    ).toBe(true);
    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
    expect(harness.detailQueryRef.refetch).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });

  it('invalidates lists and counts when an in-flight detail query overwrites a queued transition', async () => {
    const detailTest = makeRefTest('detail-query-transition', {
      status: 'PENDING_APPROVAL',
      createdAt: '2026-10-03T10:00:00.000Z',
    });
    const neighbor = makeRefTest('detail-query-neighbor', {
      status: 'PENDING',
      createdAt: '2026-10-02T10:00:00.000Z',
    });
    const updated = makeRefTest(detailTest.id, {
      status: 'PENDING',
      createdAt: '2026-10-01T10:00:00.000Z',
    });
    const harness = createHarness({}, detailTest);
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-detail-before-query', node: detailTest },
        { cursor: 'opaque-detail-neighbor', node: neighbor },
      ],
      { totalCount: 2, hasNextPage: true, endCursor: 'opaque-detail-neighbor' },
    );
    seedList(harness, 'PENDING', []);
    seedCounts(harness, { all: 2, pending: 1, pendingApproval: 1 });
    harness.detailQueryResults.next({ data: { refTest: detailTest }, loading: true });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    harness.detailData.setRefTestId(detailTest.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    await vi.waitFor(() => {
      expect(harness.detailWatch).toHaveBeenCalled();
      expect(harness.detailSubscribe).toHaveBeenCalled();
    });

    const event: TestSubscriptionEvent = {
      __typename: 'RefTestApproved',
      id: detailTest.id,
      oldStatus: 'PENDING_APPROVAL',
      status: 'PENDING',
      createdAt: updated.createdAt,
    };
    harness.detailEvents.next({ data: { refTestUpdated: event } });

    harness.cache.writeQuery({
      query: REF_TEST_DETAIL_QUERY,
      variables: { id: detailTest.id },
      data: { refTest: updated },
    });
    expect(readList(harness)?.edges.map(({ node }) => [node.id, node.createdAt])).toEqual([
      [detailTest.id, updated.createdAt],
      [neighbor.id, neighbor.createdAt],
    ]);
    harness.detailQueryResults.next({ data: { refTest: updated }, loading: false });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.detailQueryRef.refetch).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });

  it('replays a detail deletion after an in-flight detail query returns stale data', async () => {
    const detailTest = makeRefTest('detail-deleted-early', {
      status: 'PENDING',
      resultsSent: false,
    });
    const other = makeRefTest('detail-delete-other', {
      status: 'PENDING',
      resultsSent: false,
    });
    const harness = createHarness({ resultsSent: false }, detailTest);
    seedList(
      harness,
      undefined,
      [{ cursor: 'opaque-delete-other', node: other }],
      { totalCount: 2, hasNextPage: true },
    );
    seedList(
      harness,
      'PENDING',
      [{ cursor: 'opaque-delete-other', node: other }],
      { totalCount: 2, hasNextPage: true },
    );
    seedCounts(harness, { all: 2, pending: 2 });
    harness.detailQueryResults.next({ data: {}, loading: true });
    harness.detailData.setRefTestId(detailTest.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    await vi.waitFor(() => expect(harness.detailSubscribe).toHaveBeenCalled());

    harness.detailEvents.next({
      data: {
        refTestUpdated: {
          __typename: 'RefTestDeleted',
          id: detailTest.id,
        },
      },
    });
    expect(navigate).toHaveBeenCalledWith(['/ref-tests']);

    harness.cache.writeQuery({
      query: REF_TEST_DETAIL_QUERY,
      variables: { id: detailTest.id },
      data: { refTest: detailTest },
    });
    harness.detailQueryResults.next({ data: { refTest: detailTest }, loading: false });

    expect(
      harness.cache.readQuery({
        query: REF_TEST_DETAIL_QUERY,
        variables: { id: detailTest.id },
      }),
    ).toBeNull();
    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
    subscription.unsubscribe();
  });

  it('removes a deleted opened RefTest through the shared cache updater', async () => {
    const detailTest = makeRefTest('detail-delete', { status: 'PENDING' });
    const harness = createHarness({}, detailTest);
    seedList(harness, undefined, [{ cursor: 'opaque-detail-delete', node: detailTest }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-detail-delete', node: detailTest }]);
    seedCounts(harness, { all: 1, pending: 1 });
    harness.cache.writeQuery({
      query: REF_TEST_DETAIL_QUERY,
      variables: { id: detailTest.id },
      data: { refTest: detailTest },
    });
    const update = vi.spyOn(harness.cacheUpdater, 'updateCacheFromSubscription');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    harness.detailData.setRefTestId(detailTest.id);
    const subscription = TestBed.runInInjectionContext(() =>
      harness.detailData.subscribeToRefTestUpdates(testDestroyRef()).subscribe(),
    );
    await vi.waitFor(() => {
      expect(harness.detailSubscribe).toHaveBeenCalled();
    });

    const event: TestSubscriptionEvent = {
      __typename: 'RefTestDeleted',
      id: detailTest.id,
    };
    harness.detailEvents.next({ data: { refTestUpdated: event } });

    expect(update).toHaveBeenCalledWith(
      event,
      expect.objectContaining({ id: detailTest.id }),
      'detail',
    );
    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'PENDING')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(
      harness.cache.readQuery({
        query: REF_TEST_DETAIL_QUERY,
        variables: { id: detailTest.id },
      }),
    ).toBeNull();
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: detailTest.id });
    expect(
      harness.cache.readFragment({
        id: entityId,
        fragment: REF_TEST_STATE_FRAGMENT,
      }),
    ).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/ref-tests']);
    expect(harness.detailQueryRef.refetch).not.toHaveBeenCalled();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
    subscription.unsubscribe();
  });

  it('clears delete suppression after a failed mutation so a later delete event is applied', () => {
    const detailTest = makeRefTest('delete-failed', { status: 'PENDING' });
    const harness = createHarness({}, detailTest);
    seedList(harness, undefined, [{ cursor: 'opaque-delete-failed', node: detailTest }]);
    seedList(harness, 'PENDING', [{ cursor: 'opaque-delete-failed', node: detailTest }]);
    seedCounts(harness, { all: 1, pending: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    const deleteGql = TestBed.inject(DeleteRefTestsGQL) as unknown as {
      mutate: ReturnType<typeof vi.fn>;
    };
    deleteGql.mutate.mockReturnValue(throwError(() => new Error('mutation failed')));
    harness.data.deleteRefTests([detailTest.id], testDestroyRef());

    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestDeleted',
          id: detailTest.id,
          status: 'PENDING',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
  });

  it('invalidates cursor windows when revival replaces an unknown CreatedAt', () => {
    const harness = createHarness({ sortField: 'numberOfQuestions' });
    const revived = makeRefTest('revived-later', {
      status: 'EXPIRED',
      numberOfQuestions: 30,
      createdAt: '2026-01-01T10:00:00.000Z',
    });
    const inProgress = makeRefTest('in-progress-peer', {
      status: 'IN_PROGRESS',
      numberOfQuestions: 30,
      createdAt: '2026-10-02T10:00:00.000Z',
    });
    const completed = makeRefTest('completed-peer', {
      status: 'COMPLETED',
      numberOfQuestions: 30,
      createdAt: '2026-10-03T10:00:00.000Z',
    });
    seedList(harness, undefined, [{ cursor: 'opaque-revived', node: revived }], {
      totalCount: 3,
    });
    seedList(harness, 'EXPIRED', [{ cursor: 'opaque-revived', node: revived }]);
    seedList(harness, 'PENDING', []);
    seedList(harness, 'IN_PROGRESS', [{ cursor: 'opaque-in-progress', node: inProgress }]);
    seedList(harness, 'COMPLETED', [{ cursor: 'opaque-completed', node: completed }]);
    seedCounts(harness, { all: 3, expired: 1, inProgress: 1, completed: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({
      data: { refTestsUpdated: { __typename: 'RefTestRevived', id: revived.id } },
    });
    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestStarted',
          id: revived.id,
          status: 'IN_PROGRESS',
          startedAt: '2026-10-05T10:00:00.000Z',
        },
      },
    });

    // Replaced timestamps do not reuse the old cursor window while later events arrive.
    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'IN_PROGRESS')).toBeNull();
    expect(readCounts(harness)).toBeNull();

    // Even a newer timestamp is not used to reconstruct opaque connection cursors locally.
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: revived.id });
    harness.cache.writeFragment({
      id: entityId,
      fragment: REF_TEST_CREATED_AT_FRAGMENT,
      data: { __typename: 'RefTest', id: revived.id, createdAt: '2026-10-10T10:00:00.000Z' },
    });
    harness.listEvents.next({
      data: {
        refTestsUpdated: {
          __typename: 'RefTestCompleted',
          id: revived.id,
          status: 'COMPLETED',
          completedAt: '2026-10-10T10:20:00.000Z',
          questionScore: 8,
          questionTotal: 10,
          answerScore: 8,
          answerTotal: 8,
          percentage: 80,
          language: 'en',
        },
      },
    });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'COMPLETED')).toBeNull();
    expect(readList(harness, 'IN_PROGRESS')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
  });

  it('refreshes reset connections even when the response confirms an unchanged CreatedAt', () => {
    const harness = createHarness({ sortField: 'numberOfQuestions' });
    const soft = makeRefTest('soft-reset', {
      status: 'COMPLETED',
      numberOfQuestions: 30,
      createdAt: '2026-10-01T10:00:00.000Z',
    });
    const peer = makeRefTest('in-progress-peer', {
      status: 'IN_PROGRESS',
      numberOfQuestions: 30,
      createdAt: '2026-10-02T10:00:00.000Z',
    });
    seedList(harness, undefined, [{ cursor: 'opaque-soft', node: soft }], { totalCount: 2 });
    seedList(harness, 'COMPLETED', [{ cursor: 'opaque-soft', node: soft }]);
    seedList(harness, 'PENDING', []);
    seedList(harness, 'IN_PROGRESS', [{ cursor: 'opaque-peer', node: peer }]);
    seedCounts(harness, { all: 2, completed: 1, inProgress: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const mutation = stubMutation(
      harness,
      TestBed.inject(ResetRefTestsGQL),
      ResetRefTestsDocument,
    );
    harness.data.resetRefTests(
      { ids: [soft.id], resetType: 'SOFT', regenerateToken: false },
      testDestroyRef(),
    );
    const started: TestSubscriptionEvent = {
      __typename: 'RefTestStarted',
      id: soft.id,
      status: 'IN_PROGRESS',
      startedAt: '2026-10-05T10:00:00.000Z',
    };

    // The reset event cannot say whether CreatedAt was replaced, so it is distrusted until the
    // response answers that; a soft reset leaves it unchanged.
    harness.listEvents.next({ data: { refTestsUpdated: RESET.event(soft.id) } });
    mutation.respond(resetResponse(soft.id, soft.createdAt));
    harness.listEvents.next({ data: { refTestsUpdated: started } });

    expect(readList(harness)).toBeNull();
    expect(readList(harness, 'IN_PROGRESS')).toBeNull();
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.networkRequests()).toBe(0);
  });

  it('applies selected answer IDs from a detail completion event', () => {
    const harness = createHarness();
    const completed = makeRefTest('completion-selections', { status: 'IN_PROGRESS' });
    seedList(harness, undefined, [{ cursor: 'opaque-completion-selections', node: completed }]);
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: completed.id });
    const selectedAnswerIds = ['question-1:answer-2', 'question-2:answer-1'];

    harness.cache.writeFragment({
      id: entityId,
      fragment: REF_TEST_SELECTIONS_FRAGMENT,
      data: {
        __typename: 'RefTest',
        id: completed.id,
        selectedAnswerIds: [],
      },
    });
    harness.cacheUpdater.updateCacheFromSubscription(
      {
        __typename: 'RefTestCompleted',
        id: completed.id,
        status: 'COMPLETED',
        completedAt: '2026-10-01T10:20:00.000Z',
        questionScore: 8,
        questionTotal: 10,
        answerScore: 8,
        answerTotal: 10,
        percentage: 80,
        language: 'en',
        selectedAnswerIds,
      },
      undefined,
      'detail',
    );

    expect(
      harness.cache.readFragment<{ selectedAnswerIds: string[] }>({
        id: entityId,
        fragment: REF_TEST_SELECTIONS_FRAGMENT,
      })?.selectedAnswerIds,
    ).toEqual(selectedAnswerIds);
    expect(harness.networkRequests()).toBe(0);
  });

  it('clears cached answer selections when a reset event arrives', () => {
    const harness = createHarness();
    const completed = makeRefTest('reset-selections', { status: 'COMPLETED' });
    seedList(harness, undefined, [{ cursor: 'opaque-selections', node: completed }]);
    seedCounts(harness, { all: 1, completed: 1 });
    const entityId = harness.cache.identify({ __typename: 'RefTest', id: completed.id });
    harness.cache.writeFragment({
      id: entityId,
      fragment: REF_TEST_SELECTIONS_FRAGMENT,
      data: {
        __typename: 'RefTest',
        id: completed.id,
        selectedAnswerIds: ['answer-1', 'answer-2'],
      },
    });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());

    harness.listEvents.next({ data: { refTestsUpdated: RESET.event(completed.id) } });

    expect(
      harness.cache.readFragment<{ selectedAnswerIds: string[] }>({
        id: entityId,
        fragment: REF_TEST_SELECTIONS_FRAGMENT,
      })?.selectedAnswerIds,
    ).toEqual([]);
    expect(harness.networkRequests()).toBe(0);
  });

  for (const transition of [APPROVE, REJECT, RESET, REVIVE]) {
    for (const first of ['response', 'event'] as const) {
      it(`counts the ${transition.name} transition once when the ${first} arrives first`, () => {
        const harness = createHarness();
        const row = makeRefTest('row', { status: transition.source });
        seedTransition(harness, transition, row);
        harness.data.subscribeToRefTestUpdates(testDestroyRef());
        const respond = transition.start(harness, row.id);
        const event = transition.event(row.id);

        if (first === 'response') respond();
        harness.listEvents.next({ data: { refTestsUpdated: event } });
        if (first === 'event') respond();
        expectTransitionedOnce(harness, transition);

        // Both subscriptions deliver the event, and either may be redelivered.
        harness.cacheUpdater.updateCacheFromSubscription(event, undefined, 'detail');
        harness.listEvents.next({ data: { refTestsUpdated: event } });
        expectTransitionedOnce(harness, transition);
        expect(harness.networkRequests()).toBe(0);
      });
    }
  }

  for (const transition of [RESET, REVIVE]) {
    it(`counts the ${transition.name} transition once from the detail page wrapper`, () => {
      const row = makeRefTest('detail-row', { status: transition.source });
      const harness = createHarness({}, row);
      seedTransition(harness, transition, row);
      const respond = transition.startFromDetail!(harness, row.id);
      const event = transition.event(row.id);

      respond();
      harness.cacheUpdater.updateCacheFromSubscription(event, undefined, 'detail');
      expectTransitionedOnce(harness, transition);

      harness.cacheUpdater.updateCacheFromSubscription(event, undefined, 'detail');
      expectTransitionedOnce(harness, transition);
      expect(harness.networkRequests()).toBe(0);
    });
  }

  it('applies a queued event once against the state captured before its mutation', () => {
    const harness = createHarness();
    const row = makeRefTest('queued', { status: APPROVE.source });
    seedTransition(harness, APPROVE, row, { counts: false });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const respond = APPROVE.start(harness, row.id);
    const event = APPROVE.event(row.id);

    respond();
    harness.listEvents.next({ data: { refTestsUpdated: event } });
    expect(readList(harness, APPROVE.source)?.totalCount).toBe(2);

    seedCounts(harness, { all: 3, pendingApproval: 2, pending: 1 });
    harness.cacheUpdater.replayPendingEvents();
    expectTransitionedOnce(harness, APPROVE);

    harness.cacheUpdater.updateCacheFromSubscription(event, undefined, 'detail');
    harness.cacheUpdater.replayPendingEvents();
    expectTransitionedOnce(harness, APPROVE);
    expect(harness.networkRequests()).toBe(0);
  });

  it('keeps the captured state until the event of its own transition arrives', () => {
    const harness = createHarness();
    const row = makeRefTest('interleaved', { status: APPROVE.source });
    seedTransition(harness, APPROVE, row);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const respond = APPROVE.start(harness, row.id);

    respond();
    harness.listEvents.next({
      data: { refTestsUpdated: { __typename: 'RefTestInvitationSent', id: row.id } },
    });
    harness.listEvents.next({ data: { refTestsUpdated: APPROVE.event(row.id) } });

    expectTransitionedOnce(harness, APPROVE);
    expect(harness.networkRequests()).toBe(0);
  });

  it('forgets the captured state when the mutation fails', () => {
    const harness = createHarness();
    const row = makeRefTest('failed-mutation', { status: APPROVE.source });
    seedTransition(harness, APPROVE, row);
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const mutation = stubMutation(
      harness,
      TestBed.inject(ApproveRefTestsGQL),
      ApproveRefTestsDocument,
    );
    harness.data.approveRefTests([row.id], testDestroyRef());

    mutation.fail(new Error('mutation failed'));
    // Someone else approves the row and a refresh already reflects it before the event arrives.
    refreshAsTransitioned(harness, APPROVE, row.id);
    harness.listEvents.next({ data: { refTestsUpdated: APPROVE.event(row.id) } });

    expect(readCounts(harness)).toMatchObject({
      all: { totalCount: 3 },
      pendingApproval: { totalCount: 1 },
      pending: { totalCount: 2 },
    });
    expect(readList(harness)?.totalCount).toBe(3);
    expect(readList(harness, APPROVE.source)?.totalCount).toBe(1);
    expect(readList(harness, APPROVE.target)?.totalCount).toBe(2);
    expect(harness.listQueryRef.refetch).not.toHaveBeenCalled();
    expect(harness.countsQueryRef.refetch).not.toHaveBeenCalled();
    expect(harness.networkRequests()).toBe(0);
  });

  it('forgets the captured state of rows the mutation response omits', () => {
    const harness = createHarness();
    const approved = makeRefTest('approved-row', { status: APPROVE.source });
    const omitted = makeRefTest('omitted-row', { status: APPROVE.source });
    seedList(
      harness,
      undefined,
      [
        { cursor: 'opaque-approved', node: approved },
        { cursor: 'opaque-omitted', node: omitted },
      ],
      { totalCount: 4 },
    );
    seedList(harness, APPROVE.source, [], { totalCount: 3 });
    seedList(harness, APPROVE.target, [], { totalCount: 1 });
    seedCounts(harness, { all: 4, pendingApproval: 3, pending: 1 });
    harness.data.subscribeToRefTestUpdates(testDestroyRef());
    const mutation = stubMutation(
      harness,
      TestBed.inject(ApproveRefTestsGQL),
      ApproveRefTestsDocument,
    );
    harness.data.approveRefTests([approved.id, omitted.id], testDestroyRef());

    mutation.respond(approvedResponse(approved.id));
    harness.listEvents.next({ data: { refTestsUpdated: APPROVE.event(approved.id) } });
    expect(readCounts(harness)).toBeNull();
    expect(harness.listQueryRef.refetch).toHaveBeenCalledTimes(1);
    expect(harness.countsQueryRef.refetch).toHaveBeenCalledTimes(1);

    // Someone else approves the omitted row and a refresh already reflects both approvals.
    harness.cache.modify({
      id: harness.cache.identify({ __typename: 'RefTest', id: omitted.id }),
      fields: { status: () => APPROVE.target },
    });
    seedList(harness, APPROVE.source, [], { totalCount: 1 });
    seedList(harness, APPROVE.target, [], { totalCount: 3 });
    seedCounts(harness, { all: 4, pendingApproval: 1, pending: 3 });
    harness.listEvents.next({ data: { refTestsUpdated: APPROVE.event(omitted.id) } });

    expect(readCounts(harness)).toMatchObject({
      all: { totalCount: 4 },
      pendingApproval: { totalCount: 1 },
      pending: { totalCount: 3 },
    });
    expect(harness.networkRequests()).toBe(0);
  });
});
