/** Internal type. DO NOT USE DIRECTLY. */
type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
import { gql } from 'apollo-angular';
import { Injectable } from '@angular/core';
import * as Apollo from 'apollo-angular';
export type AcceptPrivacyNoticeInput = {
  noticeVersion: string;
  token: string;
};

export type ApproveRefTestsInput = {
  ids: Array<string | number>;
};

export type AuditLogDtoFilterInput = {
  actorEmail?: StringOperationFilterInput | null | undefined;
  actorName?: StringOperationFilterInput | null | undefined;
  and?: Array<AuditLogDtoFilterInput> | null | undefined;
  data?: StringOperationFilterInput | null | undefined;
  headers?: StringOperationFilterInput | null | undefined;
  id?: UuidOperationFilterInput | null | undefined;
  isArchived?: BooleanOperationFilterInput | null | undefined;
  or?: Array<AuditLogDtoFilterInput> | null | undefined;
  seqId?: LongOperationFilterInput | null | undefined;
  streamId?: StringOperationFilterInput | null | undefined;
  timestamp?: DateTimeOperationFilterInput | null | undefined;
  type?: StringOperationFilterInput | null | undefined;
  version?: LongOperationFilterInput | null | undefined;
};

export type AuditLogDtoSortInput = {
  actorEmail?: SortEnumType | null | undefined;
  actorName?: SortEnumType | null | undefined;
  data?: SortEnumType | null | undefined;
  headers?: SortEnumType | null | undefined;
  id?: SortEnumType | null | undefined;
  isArchived?: SortEnumType | null | undefined;
  seqId?: SortEnumType | null | undefined;
  streamId?: SortEnumType | null | undefined;
  timestamp?: SortEnumType | null | undefined;
  type?: SortEnumType | null | undefined;
  version?: SortEnumType | null | undefined;
};

export type BooleanOperationFilterInput = {
  eq?: boolean | null | undefined;
  neq?: boolean | null | undefined;
};

export type CompleteRefTestInput = {
  language?: string | null | undefined;
  selectedAnswerIds: Array<string>;
  token: string;
};

export type CreateRefTestsInput = {
  maxTimeInMinutes: number;
  numberOfQuestions: number;
  randomQuestionsForEachUser: boolean;
  scheduledAt?: string | null | undefined;
  sendAutomatedInvitations?: boolean;
  sendAutomatedResults?: boolean;
  specificQuestionNumbers?: Array<string> | null | undefined;
  title: TitleInput;
  users: Array<UserInput>;
};

export type DateTimeOperationFilterInput = {
  eq?: string | null | undefined;
  gt?: string | null | undefined;
  gte?: string | null | undefined;
  in?: Array<string | null | undefined> | null | undefined;
  lt?: string | null | undefined;
  lte?: string | null | undefined;
  neq?: string | null | undefined;
  ngt?: string | null | undefined;
  ngte?: string | null | undefined;
  nin?: Array<string | null | undefined> | null | undefined;
  nlt?: string | null | undefined;
  nlte?: string | null | undefined;
};

export type DeleteRefTestsInput = {
  ids: Array<string | number>;
};

export type ExtendRefTestTimeInput = {
  additionalMinutes: number;
  id: string | number;
};

export type FloatOperationFilterInput = {
  eq?: number | null | undefined;
  gt?: number | null | undefined;
  gte?: number | null | undefined;
  in?: Array<number | null | undefined> | null | undefined;
  lt?: number | null | undefined;
  lte?: number | null | undefined;
  neq?: number | null | undefined;
  ngt?: number | null | undefined;
  ngte?: number | null | undefined;
  nin?: Array<number | null | undefined> | null | undefined;
  nlt?: number | null | undefined;
  nlte?: number | null | undefined;
};

export type IntOperationFilterInput = {
  eq?: number | null | undefined;
  gt?: number | null | undefined;
  gte?: number | null | undefined;
  in?: Array<number | null | undefined> | null | undefined;
  lt?: number | null | undefined;
  lte?: number | null | undefined;
  neq?: number | null | undefined;
  ngt?: number | null | undefined;
  ngte?: number | null | undefined;
  nin?: Array<number | null | undefined> | null | undefined;
  nlt?: number | null | undefined;
  nlte?: number | null | undefined;
};

export type LongOperationFilterInput = {
  eq?: number | null | undefined;
  gt?: number | null | undefined;
  gte?: number | null | undefined;
  in?: Array<number | null | undefined> | null | undefined;
  lt?: number | null | undefined;
  lte?: number | null | undefined;
  neq?: number | null | undefined;
  ngt?: number | null | undefined;
  ngte?: number | null | undefined;
  nin?: Array<number | null | undefined> | null | undefined;
  nlt?: number | null | undefined;
  nlte?: number | null | undefined;
};

/** Filter RefTests based on Id, Email or Status */
export type RefTestFilterInput = {
  and?: Array<RefTestFilterInput> | null | undefined;
  /** Filter on answer score of the RefTest */
  answerScore?: IntOperationFilterInput | null | undefined;
  /** Filter on total possible answer score */
  answerTotal?: IntOperationFilterInput | null | undefined;
  /** Filter on completion date of the RefTest */
  completedAt?: DateTimeOperationFilterInput | null | undefined;
  /** Filter on creation date of the RefTest */
  createdAt?: DateTimeOperationFilterInput | null | undefined;
  /** Filter on email of the user who started the RefTest */
  email?: StringOperationFilterInput | null | undefined;
  /** Filter on first name of the user who started the RefTest */
  firstName?: StringOperationFilterInput | null | undefined;
  /** Filter on invitation was sent for the RefTest */
  invitationSent?: BooleanOperationFilterInput | null | undefined;
  /** Filter on whether the RefTest has been anonymized (privacy erasure/consent withdrawal) */
  isAnonymized?: BooleanOperationFilterInput | null | undefined;
  /** Filter on language where the RefTest was taken */
  language?: StringOperationFilterInput | null | undefined;
  /** Filter on last name of the user who started the RefTest */
  lastName?: StringOperationFilterInput | null | undefined;
  /** Filter on maximum time in minutes for the RefTest */
  maxTimeInMinutes?: IntOperationFilterInput | null | undefined;
  /** Filter on name of the user who started the RefTest (e.g., ) */
  name?: StringOperationFilterInput | null | undefined;
  /** Filter on number of questions in the RefTest */
  numberOfQuestions?: IntOperationFilterInput | null | undefined;
  or?: Array<RefTestFilterInput> | null | undefined;
  /** Filter on percentage of correct answers */
  percentage?: FloatOperationFilterInput | null | undefined;
  /** Filter on question score of the RefTest */
  questionScore?: IntOperationFilterInput | null | undefined;
  /** Filter on total possible question score */
  questionTotal?: IntOperationFilterInput | null | undefined;
  /** Filter on results were sent for the RefTest */
  resultsSent?: BooleanOperationFilterInput | null | undefined;
  /** Filter on scheduled date of the RefTest */
  scheduledAt?: DateTimeOperationFilterInput | null | undefined;
  /** Filter on whether invitations are sent automatically */
  sendInvitationsAutomatically?: BooleanOperationFilterInput | null | undefined;
  /** Filter on whether results are sent automatically */
  sendResultsAutomatically?: BooleanOperationFilterInput | null | undefined;
  /** Filter on start date of the RefTest */
  startedAt?: DateTimeOperationFilterInput | null | undefined;
  /** Filter on status of the RefTest (e.g., InProgress, Completed, Expired) */
  status?: RefTestStatusOperationFilterInput | null | undefined;
  /** Filter on RefTest title */
  title?: RefTestTitleFilterInput | null | undefined;
};

export type RefTestResetType =
  | 'HARD'
  | 'SOFT';

export type RefTestSessionStatus =
  | 'ACQUIRED'
  | 'BLOCKED';

/** Sort RefTests by Id, Email, Status, Creation Date, Start Date, Completion Date, Percentage, QuestionScore, Number of Questions and Maximum Time */
export type RefTestSortInput = {
  /** Sort on answer score of the RefTest */
  answerScore?: SortEnumType | null | undefined;
  /** Sort on total possible answer score */
  answerTotal?: SortEnumType | null | undefined;
  /** Sort on completion date of the RefTest */
  completedAt?: SortEnumType | null | undefined;
  /** Sort on creation date of the RefTest */
  createdAt?: SortEnumType | null | undefined;
  /** Sort on email of the user who started the RefTest */
  email?: SortEnumType | null | undefined;
  /** Sort on first name of the user who started the RefTest */
  firstName?: SortEnumType | null | undefined;
  /** Sort on invitation was sent for the RefTest */
  invitationSent?: SortEnumType | null | undefined;
  /** Sort on language where the RefTest was taken */
  language?: SortEnumType | null | undefined;
  /** Sort on last name of the user who started the RefTest */
  lastName?: SortEnumType | null | undefined;
  /** Sort on maximum time in minutes for the RefTest */
  maxTimeInMinutes?: SortEnumType | null | undefined;
  /** Sort on name of the user who started the RefTest (e.g., ) */
  name?: SortEnumType | null | undefined;
  /** Sort on number of questions in the RefTest */
  numberOfQuestions?: SortEnumType | null | undefined;
  /** Sort on percentage of correct answers */
  percentage?: SortEnumType | null | undefined;
  /** Sort on question score of the RefTest */
  questionScore?: SortEnumType | null | undefined;
  /** Sort on total possible question score */
  questionTotal?: SortEnumType | null | undefined;
  /** Sort on results were sent for the RefTest */
  resultsSent?: SortEnumType | null | undefined;
  /** Sort on scheduled date of the RefTest */
  scheduledAt?: SortEnumType | null | undefined;
  /** Sort on whether invitations are sent automatically */
  sendInvitationsAutomatically?: SortEnumType | null | undefined;
  /** Sort on whether results are sent automatically */
  sendResultsAutomatically?: SortEnumType | null | undefined;
  /** Sort on start date of the RefTest */
  startedAt?: SortEnumType | null | undefined;
  /** Sort on status of the RefTest (e.g., InProgress, Completed, Expired) */
  status?: SortEnumType | null | undefined;
};

export type RefTestStatus =
  | 'COMPLETED'
  | 'EXPIRED'
  | 'IN_PROGRESS'
  | 'PENDING'
  | 'PENDING_APPROVAL'
  | 'REJECTED';

export type RefTestStatusOperationFilterInput = {
  eq?: RefTestStatus | null | undefined;
  in?: Array<RefTestStatus> | null | undefined;
  neq?: RefTestStatus | null | undefined;
  nin?: Array<RefTestStatus> | null | undefined;
};

/** Filter RefTest titles based on Value */
export type RefTestTitleFilterInput = {
  and?: Array<RefTestTitleFilterInput> | null | undefined;
  or?: Array<RefTestTitleFilterInput> | null | undefined;
  /** Filter on RefTest title value */
  value?: StringOperationFilterInput | null | undefined;
};

/** Sort RefTest titles by Value */
export type RefTestTitleSortInput = {
  /** Sort on RefTest title value */
  value?: SortEnumType | null | undefined;
};

export type RegenerateRefTestTokenInput = {
  refTestId: string | number;
};

export type RejectRefTestsInput = {
  ids: Array<string | number>;
  reason: string;
};

export type ResetRefTestsInput = {
  ids: Array<string | number>;
  regenerateToken: boolean;
  resetType: RefTestResetType;
};

export type ReviveRefTestsInput = {
  ids: Array<string | number>;
};

export type SaveRefTestProgressInput = {
  currentQuestionIndex: number;
  language: string;
  selectedAnswerIds: Array<string>;
  token: string;
};

export type SendInvitationsInput = {
  ids: Array<string | number>;
};

export type SendReportInput = {
  ids: Array<string | number>;
};

export type SendResultsInput = {
  ids: Array<string | number>;
};

export type SortEnumType =
  | 'ASC'
  | 'DESC';

export type StartRefTestInput = {
  token: string;
};

export type StringOperationFilterInput = {
  and?: Array<StringOperationFilterInput> | null | undefined;
  contains?: string | null | undefined;
  endsWith?: string | null | undefined;
  eq?: string | null | undefined;
  in?: Array<string | null | undefined> | null | undefined;
  ncontains?: string | null | undefined;
  nendsWith?: string | null | undefined;
  neq?: string | null | undefined;
  nin?: Array<string | null | undefined> | null | undefined;
  nstartsWith?: string | null | undefined;
  or?: Array<StringOperationFilterInput> | null | undefined;
  startsWith?: string | null | undefined;
};

export type TitleInput = {
  id?: string | number | null | undefined;
  name?: string | null | undefined;
};

export type UpdateRefTestConfigurationInput = {
  id: string | number;
  maxTimeInMinutes: number;
  numberOfQuestions: number;
  randomQuestions?: boolean;
  specificQuestionNumbers?: Array<string> | null | undefined;
  title: TitleInput;
};

export type UpdateRefTestDetailsInput = {
  email: string;
  firstName: string;
  id: string | number;
  lastName: string;
  resendInvitation?: boolean;
};

export type UpdateRefTestNotificationSettingsInput = {
  id: string | number;
  sendInvitationsAutomatically?: boolean | null | undefined;
  sendResultsAutomatically?: boolean | null | undefined;
};

export type UserInput = {
  email: string;
  firstName: string;
  lastName: string;
};

export type UuidOperationFilterInput = {
  eq?: string | null | undefined;
  gt?: string | null | undefined;
  gte?: string | null | undefined;
  in?: Array<string | null | undefined> | null | undefined;
  lt?: string | null | undefined;
  lte?: string | null | undefined;
  neq?: string | null | undefined;
  ngt?: string | null | undefined;
  ngte?: string | null | undefined;
  nin?: Array<string | null | undefined> | null | undefined;
  nlt?: string | null | undefined;
  nlte?: string | null | undefined;
};

export type WithdrawConsentInput = {
  token: string;
};

export type GetAuditLogsQueryVariables = Exact<{
  first?: number | null | undefined;
  after?: string | null | undefined;
  where?: AuditLogDtoFilterInput | null | undefined;
  order?: Array<AuditLogDtoSortInput> | AuditLogDtoSortInput | null | undefined;
}>;


export type GetAuditLogsQuery = { auditLogs: { totalCount: number, edges: Array<{ cursor: string, node: { seqId: number, id: string, streamId: string, version: number, data: string | null, type: string, timestamp: string, actorName: string, actorEmail: string, headers: string | null, nodeId: string | null } }> | null, pageInfo: { hasNextPage: boolean, endCursor: string | null } } | null };

export type AcceptPrivacyNoticeMutationVariables = Exact<{
  input: AcceptPrivacyNoticeInput;
}>;


export type AcceptPrivacyNoticeMutation = { acceptPrivacyNotice: { participantRefTest: { id: string } | null, errors: Array<
      | { __typename: 'RefTestNotFoundError', message: string }
      | { __typename: 'RefTestValidationError', message: string }
    > | null } };

export type CompleteRefTestMutationVariables = Exact<{
  input: CompleteRefTestInput;
}>;


export type CompleteRefTestMutation = { completeRefTest: { participantRefTest: { id: string, questionScore: number | null, questionTotal: number, answerScore: number | null, answerTotal: number | null, percentage: number | null, sendResultsAutomatically: boolean, resultsSent: boolean } | null } };

export type SaveRefTestProgressMutationVariables = Exact<{
  input: SaveRefTestProgressInput;
}>;


export type SaveRefTestProgressMutation = { saveRefTestProgress: { participantRefTest: { id: string, currentQuestionIndex: number | null, selectedAnswerIds: Array<string> } | null } };

export type StartRefTestMutationVariables = Exact<{
  input: StartRefTestInput;
}>;


export type StartRefTestMutation = { startRefTest: { participantRefTest: { id: string, startedAt: string | null, maxTimeInMinutes: number, currentQuestionIndex: number | null, selectedAnswerIds: Array<string>, questions: Array<{ id: string, phrase: unknown, answers: Array<{ id: string, phrase: unknown }> } | null> | null } | null, errors: Array<
      | { __typename: 'InvalidRefTestStatusError', message: string }
      | { __typename: 'RefTestExpiredError', message: string }
      | { __typename: 'RefTestNotFoundError', message: string }
      | { __typename: 'RefTestValidationError' }
    > | null } };

export type WithdrawConsentMutationVariables = Exact<{
  input: WithdrawConsentInput;
}>;


export type WithdrawConsentMutation = { withdrawConsent: { boolean: boolean | null, errors: Array<
      | { __typename: 'RefTestNotFoundError', message: string }
      | { __typename: 'RefTestValidationError' }
    > | null } };

export type GetPrivacyNoticeQueryVariables = Exact<{ [key: string]: never; }>;


export type GetPrivacyNoticeQuery = { privacyNotice: { controllerName: string, controllerAddress: string, contactEmail: string, noticeVersion: string, noticeEffectiveDate: string, retentionYears: number } };

export type GetRefTestByTokenQueryVariables = Exact<{
  token: string;
}>;


export type GetRefTestByTokenQuery = { refTestByToken:
    | { __typename: 'InvalidRefTestStatusError', message: string }
    | { __typename: 'ParticipantRefTest', id: string, name: string, email: string, numberOfQuestions: number, maxTimeInMinutes: number, resultsSent: boolean, status: RefTestStatus, currentQuestionIndex: number | null, selectedAnswerIds: Array<string>, questionScore: number | null, questionTotal: number, answerScore: number | null, answerTotal: number | null, percentage: number | null, sendResultsAutomatically: boolean, questions: Array<{ id: string, number: string | null, phrase: unknown, answers: Array<{ id: string, number: string | null, phrase: unknown, isCorrect: boolean | null }> } | null> | null }
    | { __typename: 'RefTestExpiredError', message: string }
    | { __typename: 'RefTestNotFoundError', message: string }
   };

export type GetResultsEmailDelayMinutesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetResultsEmailDelayMinutesQuery = { resultsEmailDelayMinutes: number };

export type GetScoreConfigurationQueryVariables = Exact<{ [key: string]: never; }>;


export type GetScoreConfigurationQuery = { scoreConfiguration: { passingPercentage: number } };

export type RefTestSessionLockSubscriptionVariables = Exact<{
  token: string;
  sessionId: string;
}>;


export type RefTestSessionLockSubscription = { refTestSessionLock: { status: RefTestSessionStatus } };

export type RefTestTimeExtendedSubscriptionVariables = Exact<{
  id: string | number;
}>;


export type RefTestTimeExtendedSubscription = { refTestTimeExtended: { id: string, newMaxTimeInMinutes: number } };

export type ApproveRefTestsMutationVariables = Exact<{
  input: ApproveRefTestsInput;
}>;


export type ApproveRefTestsMutation = { approveRefTests: { approveRefTestsResult: { totalRequested: number, successfullyApproved: number, failed: number, approvedRefTests: Array<{ id: string, status: RefTestStatus, createdAt: string, invitationSent: boolean }>, errors: Array<{ refTestId: string, errorMessage: string }> } | null } };

export type CreateRefTestsMutationVariables = Exact<{
  input: CreateRefTestsInput;
}>;


export type CreateRefTestsMutation = { createRefTests: { createRefTestsResult: { totalRequested: number, successfullyCreated: number, failed: number, errors: Array<{ errorMessage: string, user: { firstName: string, lastName: string, email: string } }> } | null } };

export type DeleteRefTestsMutationVariables = Exact<{
  input: DeleteRefTestsInput;
}>;


export type DeleteRefTestsMutation = { deleteRefTests: { deleteRefTestsResult: { totalRequested: number, successfullyDeleted: number, failed: number, deletedRefTests: Array<{ id: string, status: RefTestStatus }>, errors: Array<{ refTestId: string, errorMessage: string }> } | null } };

export type ExtendRefTestTimeMutationVariables = Exact<{
  input: ExtendRefTestTimeInput;
}>;


export type ExtendRefTestTimeMutation = { extendRefTestTime: { refTest: { id: string, maxTimeInMinutes: number } | null, errors: Array<
      | { message: string }
      | { message: string }
    > | null } };

export type RegenerateRefTestTokenMutationVariables = Exact<{
  input: RegenerateRefTestTokenInput;
}>;


export type RegenerateRefTestTokenMutation = { regenerateRefTestToken: { refTest: { id: string } | null, errors: Array<
      | { message: string }
      | { message: string }
    > | null } };

export type RejectRefTestsMutationVariables = Exact<{
  input: RejectRefTestsInput;
}>;


export type RejectRefTestsMutation = { rejectRefTests: { rejectRefTestsResult: { totalRequested: number, successfullyRejected: number, failed: number, rejectedRefTests: Array<{ id: string, status: RefTestStatus, rejectionReason: string | null }>, errors: Array<{ refTestId: string, errorMessage: string }> } | null } };

export type ResetRefTestsMutationVariables = Exact<{
  input: ResetRefTestsInput;
}>;


export type ResetRefTestsMutation = { resetRefTests: { resetRefTestsResult: { totalRequested: number, successfullyReset: number, failed: number, resetRefTests: Array<{ id: string, status: RefTestStatus, createdAt: string, invitationSent: boolean, resultsSent: boolean, startedAt: string | null, completedAt: string | null, questionScore: number | null, answerScore: number | null, questionTotal: number, answerTotal: number | null, percentage: number | null, selectedAnswerIds: Array<string> }>, errors: Array<{ refTestId: string, errorMessage: string }> } | null } };

export type ReviveRefTestsMutationVariables = Exact<{
  input: ReviveRefTestsInput;
}>;


export type ReviveRefTestsMutation = { reviveRefTests: { reviveRefTestsResult: { totalRequested: number, successfullyRevived: number, failed: number, revivedRefTests: Array<{ id: string, status: RefTestStatus, createdAt: string, invitationSent: boolean }>, errors: Array<{ refTestId: string, errorMessage: string }> } | null } };

export type SendRefTestInvitationsMutationVariables = Exact<{
  input: SendInvitationsInput;
}>;


export type SendRefTestInvitationsMutation = { sendInvitations: { sendInvitationsResult: { totalRequested: number, successfullySent: number, failed: number, sentRefTests: Array<{ id: string, invitationSent: boolean }>, errors: Array<{ refTestId: string, errorMessage: string, user: { firstName: string, lastName: string, email: string } | null }> } | null } };

export type SendReportMutationVariables = Exact<{
  input: SendReportInput;
}>;


export type SendReportMutation = { sendReport: { sendReportResult: { success: boolean, message: string, refTestCount: number } | null } };

export type SendRefTestResultsMutationVariables = Exact<{
  input: SendResultsInput;
}>;


export type SendRefTestResultsMutation = { sendResults: { sendResultsResult: { totalRequested: number, successfullySent: number, failed: number, sentRefTests: Array<{ id: string, resultsSent: boolean }>, errors: Array<{ refTestId: string, errorMessage: string, user: { firstName: string, lastName: string, email: string } | null }> } | null } };

export type UpdateRefTestConfigurationMutationVariables = Exact<{
  input: UpdateRefTestConfigurationInput;
}>;


export type UpdateRefTestConfigurationMutation = { updateRefTestConfiguration: { refTest: { id: string, numberOfQuestions: number, maxTimeInMinutes: number, title: { id: string, value: string } | null, questions: Array<{ id: string, number: string, phrase: unknown, answers: Array<{ id: string, number: string | null, phrase: unknown, isCorrect: boolean }> } | null> | null } | null, errors: Array<
      | { message: string }
      | { message: string }
    > | null } };

export type UpdateRefTestDetailsMutationVariables = Exact<{
  input: UpdateRefTestDetailsInput;
}>;


export type UpdateRefTestDetailsMutation = { updateRefTestDetails: { refTest: { id: string, firstName: string, lastName: string, name: string, email: string } | null, errors: Array<
      | { message: string }
      | { message: string }
    > | null } };

export type UpdateRefTestNotificationSettingsMutationVariables = Exact<{
  input: UpdateRefTestNotificationSettingsInput;
}>;


export type UpdateRefTestNotificationSettingsMutation = { updateRefTestNotificationSettings: { refTest: { id: string, sendInvitationsAutomatically: boolean, sendResultsAutomatically: boolean } | null, errors: Array<{ message: string }> | null } };

export type GetEnabledLanguagesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetEnabledLanguagesQuery = { enabledLanguages: Array<string> };

export type GetQuestionsByNumberQueryVariables = Exact<{
  numbers: Array<string> | string;
}>;


export type GetQuestionsByNumberQuery = { questionsByNumber: Array<{ id: string, number: string, phrase: unknown }> };

export type GetRefTestByIdQueryVariables = Exact<{
  id: string | number;
  skipQuestions?: boolean;
}>;


export type GetRefTestByIdQuery = { refTest:
    | { __typename: 'RefTest', id: string, firstName: string, lastName: string, name: string, email: string, invitationSent: boolean, resultsSent: boolean, sendInvitationsAutomatically: boolean, sendResultsAutomatically: boolean, status: RefTestStatus, numberOfQuestions: number, maxTimeInMinutes: number, scheduledAt: string | null, startedAt: string | null, completedAt: string | null, questionScore: number | null, answerScore: number | null, questionTotal: number, answerTotal: number | null, percentage: number | null, selectedAnswerIds: Array<string>, rejectionReason: string | null, isAnonymized: boolean, anonymizedAt: string | null, language: string | null, title: { id: string, value: string } | null, questions?: Array<{ id: string, number: string, phrase: unknown, answers: Array<{ id: string, number: string | null, phrase: unknown, isCorrect: boolean }> } | null> | null }
    | { __typename: 'RefTestNotFoundError', message: string }
   };

export type GetRefTestsAllCountsQueryVariables = Exact<{
  allWhere?: RefTestFilterInput | null | undefined;
  pendingWhere?: RefTestFilterInput | null | undefined;
  inProgressWhere?: RefTestFilterInput | null | undefined;
  completedWhere?: RefTestFilterInput | null | undefined;
  expiredWhere?: RefTestFilterInput | null | undefined;
  pendingApprovalWhere?: RefTestFilterInput | null | undefined;
  rejectedWhere?: RefTestFilterInput | null | undefined;
}>;


export type GetRefTestsAllCountsQuery = { all: { totalCount: number } | null, pending: { totalCount: number } | null, inProgress: { totalCount: number } | null, completed: { totalCount: number } | null, expired: { totalCount: number } | null, pendingApproval: { totalCount: number } | null, rejected: { totalCount: number } | null };

export type GetRefTestsQueryVariables = Exact<{
  first?: number | null | undefined;
  after?: string | null | undefined;
  where?: RefTestFilterInput | null | undefined;
  order?: Array<RefTestSortInput> | RefTestSortInput | null | undefined;
}>;


export type GetRefTestsQuery = { refTests: { totalCount: number, edges: Array<{ cursor: string, node: { id: string, name: string, email: string, invitationSent: boolean, resultsSent: boolean, sendInvitationsAutomatically: boolean, sendResultsAutomatically: boolean, status: RefTestStatus, numberOfQuestions: number, maxTimeInMinutes: number, scheduledAt: string | null, startedAt: string | null, completedAt: string | null, questionScore: number | null, answerScore: number | null, questionTotal: number, answerTotal: number | null, percentage: number | null, rejectionReason: string | null, isAnonymized: boolean, title: { id: string, value: string } | null } }> | null, pageInfo: { hasNextPage: boolean, endCursor: string | null } } | null };

export type GetRefTestTitlesQueryVariables = Exact<{
  first: number;
  after?: string | null | undefined;
  where?: RefTestTitleFilterInput | null | undefined;
  order?: Array<RefTestTitleSortInput> | RefTestTitleSortInput | null | undefined;
}>;


export type GetRefTestTitlesQuery = { refTestTitles: { totalCount: number, edges: Array<{ cursor: string, node: { id: string, value: string } }> | null, pageInfo: { hasNextPage: boolean, endCursor: string | null } } | null };

export type SearchQuestionsByNumberQueryVariables = Exact<{
  number?: string | null | undefined;
}>;


export type SearchQuestionsByNumberQuery = { searchQuestionsByNumber: Array<{ id: string, number: string, phrase: unknown }> };

export type RefTestUpdatedSubscriptionVariables = Exact<{
  id: string | number;
}>;


export type RefTestUpdatedSubscription = { refTestUpdated:
    | { __typename: 'RefTestAnonymized', id: string, status: RefTestStatus, name: string, email: string }
    | { __typename: 'RefTestApproved', id: string, status: RefTestStatus, approvedAt: string }
    | { __typename: 'RefTestCompleted', id: string, status: RefTestStatus, completedAt: string, questionScore: number, questionTotal: number, answerScore: number, answerTotal: number, percentage: number, language: string }
    | { __typename: 'RefTestCreated', id: string }
    | { __typename: 'RefTestDeleted', id: string }
    | { __typename: 'RefTestExpired', id: string, status: RefTestStatus }
    | { __typename: 'RefTestInvitationSent', id: string }
    | { __typename: 'RefTestRejected', id: string, status: RefTestStatus, reason: string, rejectedAt: string }
    | { __typename: 'RefTestReset', id: string, oldStatus: RefTestStatus }
    | { __typename: 'RefTestResultSent', id: string }
    | { __typename: 'RefTestRevived', id: string }
    | { __typename: 'RefTestStarted', id: string, status: RefTestStatus, startedAt: string }
   };

export type RefTestsUpdatedSubscriptionVariables = Exact<{ [key: string]: never; }>;


export type RefTestsUpdatedSubscription = { refTestsUpdated:
    | { __typename: 'RefTestAnonymized', id: string, status: RefTestStatus, name: string, email: string }
    | { __typename: 'RefTestApproved', id: string, status: RefTestStatus, approvedAt: string }
    | { __typename: 'RefTestCompleted', id: string, status: RefTestStatus, completedAt: string, questionScore: number, questionTotal: number, answerScore: number, answerTotal: number, percentage: number, language: string }
    | { __typename: 'RefTestCreated', id: string, name: string, email: string, titleId: string | null, titleValue: string | null, invitationSent: boolean, resultsSent: boolean, sendInvitationsAutomatically: boolean, sendResultsAutomatically: boolean, status: RefTestStatus, numberOfQuestions: number, maxTimeInMinutes: number }
    | { __typename: 'RefTestDeleted', id: string, status: RefTestStatus }
    | { __typename: 'RefTestExpired', id: string, status: RefTestStatus }
    | { __typename: 'RefTestInvitationSent', id: string }
    | { __typename: 'RefTestRejected', id: string, status: RefTestStatus, reason: string, rejectedAt: string }
    | { __typename: 'RefTestReset', id: string, oldStatus: RefTestStatus }
    | { __typename: 'RefTestResultSent', id: string }
    | { __typename: 'RefTestRevived', id: string }
    | { __typename: 'RefTestStarted', id: string, status: RefTestStatus, startedAt: string }
   };

export const GetAuditLogsDocument = gql`
    query GetAuditLogs($first: Int, $after: String, $where: AuditLogDtoFilterInput, $order: [AuditLogDtoSortInput!]) {
  auditLogs(first: $first, after: $after, where: $where, order: $order) {
    edges {
      cursor
      node {
        seqId
        id
        streamId
        version
        data
        type
        timestamp
        actorName
        actorEmail
        headers
        nodeId
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

  @Injectable({
    providedIn: 'root'
  })
  export class GetAuditLogsGQL extends Apollo.Query<GetAuditLogsQuery, GetAuditLogsQueryVariables> {
    override document = GetAuditLogsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const AcceptPrivacyNoticeDocument = gql`
    mutation AcceptPrivacyNotice($input: AcceptPrivacyNoticeInput!) {
  acceptPrivacyNotice(input: $input) {
    participantRefTest {
      id
    }
    errors {
      __typename
      ... on RefTestNotFoundError {
        message
      }
      ... on RefTestValidationError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class AcceptPrivacyNoticeGQL extends Apollo.Mutation<AcceptPrivacyNoticeMutation, AcceptPrivacyNoticeMutationVariables> {
    override document = AcceptPrivacyNoticeDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const CompleteRefTestDocument = gql`
    mutation CompleteRefTest($input: CompleteRefTestInput!) {
  completeRefTest(input: $input) {
    participantRefTest {
      id
      questionScore
      questionTotal
      answerScore
      answerTotal
      percentage
      sendResultsAutomatically
      resultsSent
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class CompleteRefTestGQL extends Apollo.Mutation<CompleteRefTestMutation, CompleteRefTestMutationVariables> {
    override document = CompleteRefTestDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const SaveRefTestProgressDocument = gql`
    mutation SaveRefTestProgress($input: SaveRefTestProgressInput!) {
  saveRefTestProgress(input: $input) {
    participantRefTest {
      id
      currentQuestionIndex
      selectedAnswerIds
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class SaveRefTestProgressGQL extends Apollo.Mutation<SaveRefTestProgressMutation, SaveRefTestProgressMutationVariables> {
    override document = SaveRefTestProgressDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const StartRefTestDocument = gql`
    mutation StartRefTest($input: StartRefTestInput!) {
  startRefTest(input: $input) {
    participantRefTest {
      id
      startedAt
      maxTimeInMinutes
      currentQuestionIndex
      selectedAnswerIds
      questions {
        id
        phrase
        answers {
          id
          phrase
        }
      }
    }
    errors {
      __typename
      ... on RefTestNotFoundError {
        message
      }
      ... on RefTestExpiredError {
        message
      }
      ... on InvalidRefTestStatusError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class StartRefTestGQL extends Apollo.Mutation<StartRefTestMutation, StartRefTestMutationVariables> {
    override document = StartRefTestDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const WithdrawConsentDocument = gql`
    mutation WithdrawConsent($input: WithdrawConsentInput!) {
  withdrawConsent(input: $input) {
    boolean
    errors {
      __typename
      ... on RefTestNotFoundError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class WithdrawConsentGQL extends Apollo.Mutation<WithdrawConsentMutation, WithdrawConsentMutationVariables> {
    override document = WithdrawConsentDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetPrivacyNoticeDocument = gql`
    query GetPrivacyNotice {
  privacyNotice {
    controllerName
    controllerAddress
    contactEmail
    noticeVersion
    noticeEffectiveDate
    retentionYears
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetPrivacyNoticeGQL extends Apollo.Query<GetPrivacyNoticeQuery, GetPrivacyNoticeQueryVariables> {
    override document = GetPrivacyNoticeDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetRefTestByTokenDocument = gql`
    query GetRefTestByToken($token: String!) {
  refTestByToken(token: $token) {
    __typename
    ... on ParticipantRefTest {
      id
      name
      email
      numberOfQuestions
      maxTimeInMinutes
      resultsSent
      status
      currentQuestionIndex
      selectedAnswerIds
      questionScore
      questionTotal
      answerScore
      answerTotal
      percentage
      sendResultsAutomatically
      questions {
        id
        number
        phrase
        answers {
          id
          number
          phrase
          isCorrect
        }
      }
    }
    ... on RefTestNotFoundError {
      message
    }
    ... on RefTestExpiredError {
      message
    }
    ... on InvalidRefTestStatusError {
      message
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetRefTestByTokenGQL extends Apollo.Query<GetRefTestByTokenQuery, GetRefTestByTokenQueryVariables> {
    override document = GetRefTestByTokenDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetResultsEmailDelayMinutesDocument = gql`
    query getResultsEmailDelayMinutes {
  resultsEmailDelayMinutes
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetResultsEmailDelayMinutesGQL extends Apollo.Query<GetResultsEmailDelayMinutesQuery, GetResultsEmailDelayMinutesQueryVariables> {
    override document = GetResultsEmailDelayMinutesDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetScoreConfigurationDocument = gql`
    query GetScoreConfiguration {
  scoreConfiguration {
    passingPercentage
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetScoreConfigurationGQL extends Apollo.Query<GetScoreConfigurationQuery, GetScoreConfigurationQueryVariables> {
    override document = GetScoreConfigurationDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const RefTestSessionLockDocument = gql`
    subscription RefTestSessionLock($token: String!, $sessionId: String!) {
  refTestSessionLock(token: $token, sessionId: $sessionId) {
    status
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class RefTestSessionLockGQL extends Apollo.Subscription<RefTestSessionLockSubscription, RefTestSessionLockSubscriptionVariables> {
    override document = RefTestSessionLockDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const RefTestTimeExtendedDocument = gql`
    subscription RefTestTimeExtended($id: ID!) {
  refTestTimeExtended(id: $id) {
    id
    newMaxTimeInMinutes
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class RefTestTimeExtendedGQL extends Apollo.Subscription<RefTestTimeExtendedSubscription, RefTestTimeExtendedSubscriptionVariables> {
    override document = RefTestTimeExtendedDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const ApproveRefTestsDocument = gql`
    mutation ApproveRefTests($input: ApproveRefTestsInput!) {
  approveRefTests(input: $input) {
    approveRefTestsResult {
      totalRequested
      successfullyApproved
      failed
      approvedRefTests {
        id
        status
        createdAt
        invitationSent
      }
      errors {
        refTestId
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class ApproveRefTestsGQL extends Apollo.Mutation<ApproveRefTestsMutation, ApproveRefTestsMutationVariables> {
    override document = ApproveRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const CreateRefTestsDocument = gql`
    mutation CreateRefTests($input: CreateRefTestsInput!) {
  createRefTests(input: $input) {
    createRefTestsResult {
      totalRequested
      successfullyCreated
      failed
      errors {
        user {
          firstName
          lastName
          email
        }
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class CreateRefTestsGQL extends Apollo.Mutation<CreateRefTestsMutation, CreateRefTestsMutationVariables> {
    override document = CreateRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const DeleteRefTestsDocument = gql`
    mutation DeleteRefTests($input: DeleteRefTestsInput!) {
  deleteRefTests(input: $input) {
    deleteRefTestsResult {
      totalRequested
      successfullyDeleted
      failed
      deletedRefTests {
        id
        status
      }
      errors {
        refTestId
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class DeleteRefTestsGQL extends Apollo.Mutation<DeleteRefTestsMutation, DeleteRefTestsMutationVariables> {
    override document = DeleteRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const ExtendRefTestTimeDocument = gql`
    mutation ExtendRefTestTime($input: ExtendRefTestTimeInput!) {
  extendRefTestTime(input: $input) {
    refTest {
      id
      maxTimeInMinutes
    }
    errors {
      ... on RefTestNotFoundError {
        message
      }
      ... on InvalidRefTestStatusError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class ExtendRefTestTimeGQL extends Apollo.Mutation<ExtendRefTestTimeMutation, ExtendRefTestTimeMutationVariables> {
    override document = ExtendRefTestTimeDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const RegenerateRefTestTokenDocument = gql`
    mutation RegenerateRefTestToken($input: RegenerateRefTestTokenInput!) {
  regenerateRefTestToken(input: $input) {
    refTest {
      id
    }
    errors {
      ... on RefTestNotFoundError {
        message
      }
      ... on InvalidRefTestStatusError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class RegenerateRefTestTokenGQL extends Apollo.Mutation<RegenerateRefTestTokenMutation, RegenerateRefTestTokenMutationVariables> {
    override document = RegenerateRefTestTokenDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const RejectRefTestsDocument = gql`
    mutation RejectRefTests($input: RejectRefTestsInput!) {
  rejectRefTests(input: $input) {
    rejectRefTestsResult {
      totalRequested
      successfullyRejected
      failed
      rejectedRefTests {
        id
        status
        rejectionReason
      }
      errors {
        refTestId
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class RejectRefTestsGQL extends Apollo.Mutation<RejectRefTestsMutation, RejectRefTestsMutationVariables> {
    override document = RejectRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const ResetRefTestsDocument = gql`
    mutation ResetRefTests($input: ResetRefTestsInput!) {
  resetRefTests(input: $input) {
    resetRefTestsResult {
      totalRequested
      successfullyReset
      failed
      resetRefTests {
        id
        status
        createdAt
        invitationSent
        resultsSent
        startedAt
        completedAt
        questionScore
        answerScore
        questionTotal
        answerTotal
        percentage
        selectedAnswerIds
      }
      errors {
        refTestId
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class ResetRefTestsGQL extends Apollo.Mutation<ResetRefTestsMutation, ResetRefTestsMutationVariables> {
    override document = ResetRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const ReviveRefTestsDocument = gql`
    mutation ReviveRefTests($input: ReviveRefTestsInput!) {
  reviveRefTests(input: $input) {
    reviveRefTestsResult {
      totalRequested
      successfullyRevived
      failed
      revivedRefTests {
        id
        status
        createdAt
        invitationSent
      }
      errors {
        refTestId
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class ReviveRefTestsGQL extends Apollo.Mutation<ReviveRefTestsMutation, ReviveRefTestsMutationVariables> {
    override document = ReviveRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const SendRefTestInvitationsDocument = gql`
    mutation SendRefTestInvitations($input: SendInvitationsInput!) {
  sendInvitations(input: $input) {
    sendInvitationsResult {
      totalRequested
      successfullySent
      failed
      sentRefTests {
        id
        invitationSent
      }
      errors {
        refTestId
        user {
          firstName
          lastName
          email
        }
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class SendRefTestInvitationsGQL extends Apollo.Mutation<SendRefTestInvitationsMutation, SendRefTestInvitationsMutationVariables> {
    override document = SendRefTestInvitationsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const SendReportDocument = gql`
    mutation sendReport($input: SendReportInput!) {
  sendReport(input: $input) {
    sendReportResult {
      success
      message
      refTestCount
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class SendReportGQL extends Apollo.Mutation<SendReportMutation, SendReportMutationVariables> {
    override document = SendReportDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const SendRefTestResultsDocument = gql`
    mutation SendRefTestResults($input: SendResultsInput!) {
  sendResults(input: $input) {
    sendResultsResult {
      totalRequested
      successfullySent
      failed
      sentRefTests {
        id
        resultsSent
      }
      errors {
        refTestId
        user {
          firstName
          lastName
          email
        }
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class SendRefTestResultsGQL extends Apollo.Mutation<SendRefTestResultsMutation, SendRefTestResultsMutationVariables> {
    override document = SendRefTestResultsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const UpdateRefTestConfigurationDocument = gql`
    mutation UpdateRefTestConfiguration($input: UpdateRefTestConfigurationInput!) {
  updateRefTestConfiguration(input: $input) {
    refTest {
      id
      title {
        id
        value
      }
      numberOfQuestions
      maxTimeInMinutes
      questions(includeNumber: true, includeIsCorrect: true, randomAnswerOrder: false) {
        id
        number
        phrase
        answers {
          id
          number
          phrase
          isCorrect
        }
      }
    }
    errors {
      ... on RefTestNotFoundError {
        message
      }
      ... on InvalidRefTestStatusError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class UpdateRefTestConfigurationGQL extends Apollo.Mutation<UpdateRefTestConfigurationMutation, UpdateRefTestConfigurationMutationVariables> {
    override document = UpdateRefTestConfigurationDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const UpdateRefTestDetailsDocument = gql`
    mutation UpdateRefTestDetails($input: UpdateRefTestDetailsInput!) {
  updateRefTestDetails(input: $input) {
    refTest {
      id
      firstName
      lastName
      name
      email
    }
    errors {
      ... on RefTestNotFoundError {
        message
      }
      ... on InvalidRefTestStatusError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class UpdateRefTestDetailsGQL extends Apollo.Mutation<UpdateRefTestDetailsMutation, UpdateRefTestDetailsMutationVariables> {
    override document = UpdateRefTestDetailsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const UpdateRefTestNotificationSettingsDocument = gql`
    mutation UpdateRefTestNotificationSettings($input: UpdateRefTestNotificationSettingsInput!) {
  updateRefTestNotificationSettings(input: $input) {
    refTest {
      id
      sendInvitationsAutomatically
      sendResultsAutomatically
    }
    errors {
      ... on RefTestNotFoundError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class UpdateRefTestNotificationSettingsGQL extends Apollo.Mutation<UpdateRefTestNotificationSettingsMutation, UpdateRefTestNotificationSettingsMutationVariables> {
    override document = UpdateRefTestNotificationSettingsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetEnabledLanguagesDocument = gql`
    query GetEnabledLanguages {
  enabledLanguages
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetEnabledLanguagesGQL extends Apollo.Query<GetEnabledLanguagesQuery, GetEnabledLanguagesQueryVariables> {
    override document = GetEnabledLanguagesDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetQuestionsByNumberDocument = gql`
    query GetQuestionsByNumber($numbers: [String!]!) {
  questionsByNumber(numbers: $numbers) {
    id
    number
    phrase
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetQuestionsByNumberGQL extends Apollo.Query<GetQuestionsByNumberQuery, GetQuestionsByNumberQueryVariables> {
    override document = GetQuestionsByNumberDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetRefTestByIdDocument = gql`
    query GetRefTestById($id: ID!, $skipQuestions: Boolean! = false) {
  refTest(id: $id) {
    __typename
    ... on RefTest {
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
      selectedAnswerIds
      rejectionReason
      isAnonymized
      anonymizedAt
      questions(includeNumber: true, includeIsCorrect: true, randomAnswerOrder: false) @skip(if: $skipQuestions) {
        id
        number
        phrase
        answers {
          id
          number
          phrase
          isCorrect
        }
      }
      language
    }
    ... on RefTestNotFoundError {
      message
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetRefTestByIdGQL extends Apollo.Query<GetRefTestByIdQuery, GetRefTestByIdQueryVariables> {
    override document = GetRefTestByIdDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetRefTestsAllCountsDocument = gql`
    query GetRefTestsAllCounts($allWhere: RefTestFilterInput, $pendingWhere: RefTestFilterInput, $inProgressWhere: RefTestFilterInput, $completedWhere: RefTestFilterInput, $expiredWhere: RefTestFilterInput, $pendingApprovalWhere: RefTestFilterInput, $rejectedWhere: RefTestFilterInput) {
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

  @Injectable({
    providedIn: 'root'
  })
  export class GetRefTestsAllCountsGQL extends Apollo.Query<GetRefTestsAllCountsQuery, GetRefTestsAllCountsQueryVariables> {
    override document = GetRefTestsAllCountsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetRefTestsDocument = gql`
    query GetRefTests($first: Int, $after: String, $where: RefTestFilterInput, $order: [RefTestSortInput!]) {
  refTests(first: $first, after: $after, where: $where, order: $order) {
    edges {
      cursor
      node {
        id
        title {
          id
          value
        }
        name
        email
        invitationSent
        resultsSent
        sendInvitationsAutomatically
        sendResultsAutomatically
        status
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

  @Injectable({
    providedIn: 'root'
  })
  export class GetRefTestsGQL extends Apollo.Query<GetRefTestsQuery, GetRefTestsQueryVariables> {
    override document = GetRefTestsDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetRefTestTitlesDocument = gql`
    query GetRefTestTitles($first: Int!, $after: String, $where: RefTestTitleFilterInput, $order: [RefTestTitleSortInput!]) {
  refTestTitles(first: $first, after: $after, where: $where, order: $order) {
    edges {
      cursor
      node {
        id
        value
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

  @Injectable({
    providedIn: 'root'
  })
  export class GetRefTestTitlesGQL extends Apollo.Query<GetRefTestTitlesQuery, GetRefTestTitlesQueryVariables> {
    override document = GetRefTestTitlesDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const SearchQuestionsByNumberDocument = gql`
    query SearchQuestionsByNumber($number: String) {
  searchQuestionsByNumber(number: $number) {
    id
    number
    phrase
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class SearchQuestionsByNumberGQL extends Apollo.Query<SearchQuestionsByNumberQuery, SearchQuestionsByNumberQueryVariables> {
    override document = SearchQuestionsByNumberDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const RefTestUpdatedDocument = gql`
    subscription RefTestUpdated($id: ID!) {
  refTestUpdated(id: $id) {
    __typename
    ... on RefTestStarted {
      id
      status
      startedAt
    }
    ... on RefTestCompleted {
      id
      status
      completedAt
      questionScore
      questionTotal
      answerScore
      answerTotal
      percentage
      language
    }
    ... on RefTestExpired {
      id
      status
    }
    ... on RefTestInvitationSent {
      id
    }
    ... on RefTestResultSent {
      id
    }
    ... on RefTestDeleted {
      id
    }
    ... on RefTestAnonymized {
      id
      status
      name
      email
    }
    ... on RefTestReset {
      id
      oldStatus
    }
    ... on RefTestRevived {
      id
    }
    ... on RefTestCreated {
      id
    }
    ... on RefTestApproved {
      id
      status
      approvedAt
    }
    ... on RefTestRejected {
      id
      status
      reason
      rejectedAt
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class RefTestUpdatedGQL extends Apollo.Subscription<RefTestUpdatedSubscription, RefTestUpdatedSubscriptionVariables> {
    override document = RefTestUpdatedDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const RefTestsUpdatedDocument = gql`
    subscription RefTestsUpdated {
  refTestsUpdated {
    __typename
    ... on RefTestStarted {
      id
      status
      startedAt
    }
    ... on RefTestCompleted {
      id
      status
      completedAt
      questionScore
      questionTotal
      answerScore
      answerTotal
      percentage
      language
    }
    ... on RefTestExpired {
      id
      status
    }
    ... on RefTestInvitationSent {
      id
    }
    ... on RefTestResultSent {
      id
    }
    ... on RefTestDeleted {
      id
      status
    }
    ... on RefTestAnonymized {
      id
      status
      name
      email
    }
    ... on RefTestReset {
      id
      oldStatus
    }
    ... on RefTestRevived {
      id
    }
    ... on RefTestCreated {
      id
      name
      email
      titleId
      titleValue
      invitationSent
      resultsSent
      sendInvitationsAutomatically
      sendResultsAutomatically
      status
      numberOfQuestions
      maxTimeInMinutes
    }
    ... on RefTestApproved {
      id
      status
      approvedAt
    }
    ... on RefTestRejected {
      id
      status
      reason
      rejectedAt
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class RefTestsUpdatedGQL extends Apollo.Subscription<RefTestsUpdatedSubscription, RefTestsUpdatedSubscriptionVariables> {
    override document = RefTestsUpdatedDocument;

    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }