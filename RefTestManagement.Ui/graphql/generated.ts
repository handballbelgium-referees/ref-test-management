import { gql } from 'apollo-angular';
import { Injectable } from '@angular/core';
import * as Apollo from 'apollo-angular';
export type Maybe<T> = T | null;
export type InputMaybe<T> = Maybe<T>;
export type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
export type MakeOptional<T, K extends keyof T> = Omit<T, K> & { [SubKey in K]?: Maybe<T[SubKey]> };
export type MakeMaybe<T, K extends keyof T> = Omit<T, K> & { [SubKey in K]: Maybe<T[SubKey]> };
export type MakeEmpty<T extends { [key: string]: unknown }, K extends keyof T> = { [_ in K]?: never };
export type Incremental<T> = T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
/** All built-in and custom scalars, mapped to their actual values */
export type Scalars = {
  ID: { input: string; output: string; }
  String: { input: string; output: string; }
  Boolean: { input: boolean; output: boolean; }
  Int: { input: number; output: number; }
  Float: { input: number; output: number; }
  /** The `DateTime` scalar represents an ISO-8601 compliant date time type. */
  DateTime: { input: string; output: string; }
  JSON: { input: Record<string, string>; output: Record<string, string>; }
  UUID: { input: string; output: string; }
};

/** IHF RefTest answer */
export type Answer = {
  __typename?: 'Answer';
  /** Answer id */
  id: Scalars['String']['output'];
  /** Answer is correct */
  isCorrect: Scalars['Boolean']['output'];
  /** Answer number */
  number?: Maybe<Scalars['String']['output']>;
  /** Translations of the answer phrase */
  phrase?: Maybe<Scalars['JSON']['output']>;
};

/** Defines when a policy shall be executed. */
export enum ApplyPolicy {
  /** After the resolver was executed. */
  AfterResolver = 'AFTER_RESOLVER',
  /** Before the resolver was executed. */
  BeforeResolver = 'BEFORE_RESOLVER',
  /** The policy is applied in the validation step before the execution. */
  Validation = 'VALIDATION'
}

export type BooleanOperationFilterInput = {
  eq?: InputMaybe<Scalars['Boolean']['input']>;
  neq?: InputMaybe<Scalars['Boolean']['input']>;
};

export type BulkCreationError = {
  __typename?: 'BulkCreationError';
  errorMessage: Scalars['String']['output'];
  user: User;
};

export type BulkRefTestsResult = {
  __typename?: 'BulkRefTestsResult';
  createdRefTests: Array<RefTest>;
  errors: Array<BulkCreationError>;
  failed: Scalars['Int']['output'];
  successfullyCreated: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

/** The scope of a cache hint. */
export enum CacheControlScope {
  /** The value to cache is specific to a single user. */
  Private = 'PRIVATE',
  /** The value to cache is not tied to a single user. */
  Public = 'PUBLIC'
}

export type CompleteRefTestError = InvalidRefTestStatusError | RefTestNotFoundError;

export type CompleteRefTestInput = {
  language?: InputMaybe<Scalars['String']['input']>;
  selectedAnswerIds: Array<Scalars['String']['input']>;
  token: Scalars['String']['input'];
};

export type CompleteRefTestPayload = {
  __typename?: 'CompleteRefTestPayload';
  errors?: Maybe<Array<CompleteRefTestError>>;
  refTest?: Maybe<RefTest>;
};

export type CreateBulkRefTestsInput = {
  maxTimeInMinutes: Scalars['Int']['input'];
  numberOfQuestions: Scalars['Int']['input'];
  randomQuestionsForEachUser: Scalars['Boolean']['input'];
  sendAutomatedInvitations?: Scalars['Boolean']['input'];
  sendAutomatedResults?: Scalars['Boolean']['input'];
  specificQuestionNumbers?: InputMaybe<Array<Scalars['String']['input']>>;
  title: TitleInput;
  users: Array<UserInput>;
};

export type CreateBulkRefTestsPayload = {
  __typename?: 'CreateBulkRefTestsPayload';
  bulkRefTestsResult?: Maybe<BulkRefTestsResult>;
};

export type DateTimeOperationFilterInput = {
  eq?: InputMaybe<Scalars['DateTime']['input']>;
  gt?: InputMaybe<Scalars['DateTime']['input']>;
  gte?: InputMaybe<Scalars['DateTime']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['DateTime']['input']>>>;
  lt?: InputMaybe<Scalars['DateTime']['input']>;
  lte?: InputMaybe<Scalars['DateTime']['input']>;
  neq?: InputMaybe<Scalars['DateTime']['input']>;
  ngt?: InputMaybe<Scalars['DateTime']['input']>;
  ngte?: InputMaybe<Scalars['DateTime']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['DateTime']['input']>>>;
  nlt?: InputMaybe<Scalars['DateTime']['input']>;
  nlte?: InputMaybe<Scalars['DateTime']['input']>;
};

export type DeleteRefTestError = {
  __typename?: 'DeleteRefTestError';
  errorMessage: Scalars['String']['output'];
  refTestId: Scalars['UUID']['output'];
};

export type DeleteRefTestsInput = {
  ids: Array<Scalars['ID']['input']>;
};

export type DeleteRefTestsPayload = {
  __typename?: 'DeleteRefTestsPayload';
  deleteRefTestsResult?: Maybe<DeleteRefTestsResult>;
};

export type DeleteRefTestsResult = {
  __typename?: 'DeleteRefTestsResult';
  deletedRefTests: Array<RefTest>;
  errors: Array<DeleteRefTestError>;
  failed: Scalars['Int']['output'];
  successfullyDeleted: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

export type Error = {
  message: Scalars['String']['output'];
};

export type FloatOperationFilterInput = {
  eq?: InputMaybe<Scalars['Float']['input']>;
  gt?: InputMaybe<Scalars['Float']['input']>;
  gte?: InputMaybe<Scalars['Float']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['Float']['input']>>>;
  lt?: InputMaybe<Scalars['Float']['input']>;
  lte?: InputMaybe<Scalars['Float']['input']>;
  neq?: InputMaybe<Scalars['Float']['input']>;
  ngt?: InputMaybe<Scalars['Float']['input']>;
  ngte?: InputMaybe<Scalars['Float']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['Float']['input']>>>;
  nlt?: InputMaybe<Scalars['Float']['input']>;
  nlte?: InputMaybe<Scalars['Float']['input']>;
};

export type GenerateRefTestsReportInput = {
  ids: Array<Scalars['ID']['input']>;
};

export type GenerateRefTestsReportPayload = {
  __typename?: 'GenerateRefTestsReportPayload';
  generateReportResult?: Maybe<GenerateReportResult>;
};

export type GenerateReportResult = {
  __typename?: 'GenerateReportResult';
  message: Scalars['String']['output'];
  refTestCount: Scalars['Int']['output'];
  success: Scalars['Boolean']['output'];
};

export type IntOperationFilterInput = {
  eq?: InputMaybe<Scalars['Int']['input']>;
  gt?: InputMaybe<Scalars['Int']['input']>;
  gte?: InputMaybe<Scalars['Int']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['Int']['input']>>>;
  lt?: InputMaybe<Scalars['Int']['input']>;
  lte?: InputMaybe<Scalars['Int']['input']>;
  neq?: InputMaybe<Scalars['Int']['input']>;
  ngt?: InputMaybe<Scalars['Int']['input']>;
  ngte?: InputMaybe<Scalars['Int']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['Int']['input']>>>;
  nlt?: InputMaybe<Scalars['Int']['input']>;
  nlte?: InputMaybe<Scalars['Int']['input']>;
};

export type InvalidRefTestStatusError = Error & {
  __typename?: 'InvalidRefTestStatusError';
  message: Scalars['String']['output'];
};

export type Mutation = {
  __typename?: 'Mutation';
  completeRefTest: CompleteRefTestPayload;
  createBulkRefTests: CreateBulkRefTestsPayload;
  deleteRefTests: DeleteRefTestsPayload;
  generateRefTestsReport: GenerateRefTestsReportPayload;
  saveRefTestProgress: SaveRefTestProgressPayload;
  sendInvitations: SendInvitationsPayload;
  sendResults: SendResultsPayload;
  startRefTest: StartRefTestPayload;
};


export type MutationCompleteRefTestArgs = {
  input: CompleteRefTestInput;
};


export type MutationCreateBulkRefTestsArgs = {
  input: CreateBulkRefTestsInput;
};


export type MutationDeleteRefTestsArgs = {
  input: DeleteRefTestsInput;
};


export type MutationGenerateRefTestsReportArgs = {
  input: GenerateRefTestsReportInput;
};


export type MutationSaveRefTestProgressArgs = {
  input: SaveRefTestProgressInput;
};


export type MutationSendInvitationsArgs = {
  input: SendInvitationsInput;
};


export type MutationSendResultsArgs = {
  input: SendResultsInput;
};


export type MutationStartRefTestArgs = {
  input: StartRefTestInput;
};

/** The node interface is implemented by entities that have a global unique identifier. */
export type Node = {
  id: Scalars['ID']['output'];
};

/** Information about pagination in a connection. */
export type PageInfo = {
  __typename?: 'PageInfo';
  /** When paginating forwards, the cursor to continue. */
  endCursor?: Maybe<Scalars['String']['output']>;
  /** Indicates whether more edges exist following the set defined by the clients arguments. */
  hasNextPage: Scalars['Boolean']['output'];
  /** Indicates whether more edges exist prior the set defined by the clients arguments. */
  hasPreviousPage: Scalars['Boolean']['output'];
  /** When paginating backwards, the cursor to continue. */
  startCursor?: Maybe<Scalars['String']['output']>;
};

export type Query = {
  __typename?: 'Query';
  enabledLanguages: Array<Scalars['String']['output']>;
  /** Fetches an object given its ID. */
  node?: Maybe<Node>;
  /** Lookup nodes by a list of IDs. */
  nodes: Array<Maybe<Node>>;
  questionsByNumber: Array<Question>;
  refTest?: Maybe<RefTest>;
  refTestByToken: RefTestByTokenResult;
  refTestTitles?: Maybe<RefTestTitlesConnection>;
  refTests?: Maybe<RefTestsConnection>;
  resultsEmailDelayMinutes: Scalars['Int']['output'];
  scoreConfiguration: ScoreConfiguration;
  searchQuestionsByNumber: Array<Question>;
};


export type QueryNodeArgs = {
  id: Scalars['ID']['input'];
};


export type QueryNodesArgs = {
  ids: Array<Scalars['ID']['input']>;
};


export type QueryQuestionsByNumberArgs = {
  numbers: Array<Scalars['String']['input']>;
};


export type QueryRefTestArgs = {
  id: Scalars['ID']['input'];
};


export type QueryRefTestByTokenArgs = {
  token: Scalars['String']['input'];
};


export type QueryRefTestTitlesArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<RefTestTitleSortInput>>;
  where?: InputMaybe<RefTestTitleFilterInput>;
};


export type QueryRefTestsArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<RefTestSortInput>>;
  where?: InputMaybe<RefTestFilterInput>;
};


export type QuerySearchQuestionsByNumberArgs = {
  number?: InputMaybe<Scalars['String']['input']>;
};

/** IHF RefTest question */
export type Question = {
  __typename?: 'Question';
  /** Answers for this RefTest question */
  answers: Array<Answer>;
  /** Question id */
  id: Scalars['String']['output'];
  /** Question number */
  number: Scalars['String']['output'];
  /** Translations of the question phrase */
  phrase?: Maybe<Scalars['JSON']['output']>;
};

/** RefTest */
export type RefTest = Node & {
  __typename?: 'RefTest';
  /** Score based on individual answers */
  answerScore?: Maybe<Scalars['Int']['output']>;
  /** Total possible answer score */
  answerTotal?: Maybe<Scalars['Int']['output']>;
  /** Completion date and time of the RefTest */
  completedAt?: Maybe<Scalars['DateTime']['output']>;
  /** Creation date and time of the RefTest */
  createdAt: Scalars['DateTime']['output'];
  /** Index of the current question */
  currentQuestionIndex?: Maybe<Scalars['Int']['output']>;
  /** Email of the user who started the RefTest */
  email: Scalars['String']['output'];
  /** The RefTest id */
  id: Scalars['ID']['output'];
  /** Indication of invitation was sent */
  invitationSent: Scalars['Boolean']['output'];
  /** Maximum time in minutes for the RefTest */
  maxTimeInMinutes: Scalars['Int']['output'];
  /** Name of the user who started the RefTest (e.g., ) */
  name: Scalars['String']['output'];
  /** Number of questions in the RefTest */
  numberOfQuestions: Scalars['Int']['output'];
  /** Percentage of correct answers */
  percentage?: Maybe<Scalars['Float']['output']>;
  /** Score based on fully correct questions */
  questionScore?: Maybe<Scalars['Int']['output']>;
  /** Total possible question score */
  questionTotal: Scalars['Int']['output'];
  /** Questions for this RefTest */
  questions?: Maybe<Array<Maybe<Question>>>;
  /** Indication of results were sent */
  resultsSent: Scalars['Boolean']['output'];
  /** List of selected answer IDs */
  selectedAnswerIds: Array<Scalars['String']['output']>;
  /** Start date and time of the RefTest */
  startedAt?: Maybe<Scalars['DateTime']['output']>;
  /** Status of the RefTest (e.g., InProgress, Completed, Expired). Expired tests are automatically processed by a background service. */
  status: RefTestStatus;
  /** Title of the RefTest */
  title?: Maybe<RefTestTitle>;
  /** List of answer IDs that were answered incorrectly */
  wrongAnswerIds: Array<Scalars['String']['output']>;
  /** List of question IDs that were answered incorrectly */
  wrongQuestionIds: Array<Scalars['String']['output']>;
};


/** RefTest */
export type RefTestQuestionsArgs = {
  includeIsCorrect?: InputMaybe<Scalars['Boolean']['input']>;
  includeNumber?: InputMaybe<Scalars['Boolean']['input']>;
  randomAnswerOrder?: InputMaybe<Scalars['Boolean']['input']>;
};

export type RefTestByTokenResult = InvalidRefTestStatusError | RefTest | RefTestExpiredError | RefTestNotFoundError;

export type RefTestExpiredError = Error & {
  __typename?: 'RefTestExpiredError';
  message: Scalars['String']['output'];
};

/** Filter RefTests based on Id, Email or Status */
export type RefTestFilterInput = {
  and?: InputMaybe<Array<RefTestFilterInput>>;
  /** Filter on answer score of the RefTest */
  answerScore?: InputMaybe<IntOperationFilterInput>;
  /** Filter on total possible answer score */
  answerTotal?: InputMaybe<IntOperationFilterInput>;
  /** Filter on completion date of the RefTest */
  completedAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on creation date of the RefTest */
  createdAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on email of the user who started the RefTest */
  email?: InputMaybe<StringOperationFilterInput>;
  /** Filter on first name of the user who started the RefTest */
  firstName?: InputMaybe<StringOperationFilterInput>;
  /** Filter on RefTest id */
  id?: InputMaybe<UuidOperationFilterInput>;
  /** Filter on invitation was sent for the RefTest */
  invitationSent?: InputMaybe<BooleanOperationFilterInput>;
  /** Filter on last name of the user who started the RefTest */
  lastName?: InputMaybe<StringOperationFilterInput>;
  /** Filter on maximum time in minutes for the RefTest */
  maxTimeInMinutes?: InputMaybe<IntOperationFilterInput>;
  /** Filter on number of questions in the RefTest */
  numberOfQuestions?: InputMaybe<IntOperationFilterInput>;
  or?: InputMaybe<Array<RefTestFilterInput>>;
  /** Filter on percentage of correct answers */
  percentage?: InputMaybe<FloatOperationFilterInput>;
  /** Filter on question score of the RefTest */
  questionScore?: InputMaybe<IntOperationFilterInput>;
  /** Filter on total possible question score */
  questionTotal?: InputMaybe<IntOperationFilterInput>;
  /** Filter on results were sent for the RefTest */
  resultsSent?: InputMaybe<BooleanOperationFilterInput>;
  /** Filter on start date of the RefTest */
  startedAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on status of the RefTest (e.g., InProgress, Completed, Expired) */
  status?: InputMaybe<RefTestStatusOperationFilterInput>;
  /** Filter on RefTest title */
  title?: InputMaybe<RefTestTitleFilterInput>;
};

export type RefTestNotFoundError = Error & {
  __typename?: 'RefTestNotFoundError';
  message: Scalars['String']['output'];
};

/** Sort RefTests by Id, Email, Status, Creation Date, Start Date, Completion Date, Percentage, QuestionScore, Number of Questions and Maximum Time */
export type RefTestSortInput = {
  /** Sort on answer score of the RefTest */
  answerScore?: InputMaybe<SortEnumType>;
  /** Sort on total possible answer score */
  answerTotal?: InputMaybe<SortEnumType>;
  /** Sort on completion date of the RefTest */
  completedAt?: InputMaybe<SortEnumType>;
  /** Sort on creation date of the RefTest */
  createdAt?: InputMaybe<SortEnumType>;
  /** Sort on email of the user who started the RefTest */
  email?: InputMaybe<SortEnumType>;
  /** Sort on first name of the user who started the RefTest */
  firstName?: InputMaybe<SortEnumType>;
  /** Sort on RefTest id */
  id?: InputMaybe<SortEnumType>;
  /** Sort on invitation was sent for the RefTest */
  invitationSent?: InputMaybe<SortEnumType>;
  /** Sort on last name of the user who started the RefTest */
  lastName?: InputMaybe<SortEnumType>;
  /** Sort on maximum time in minutes for the RefTest */
  maxTimeInMinutes?: InputMaybe<SortEnumType>;
  /** Sort on number of questions in the RefTest */
  numberOfQuestions?: InputMaybe<SortEnumType>;
  /** Sort on percentage of correct answers */
  percentage?: InputMaybe<SortEnumType>;
  /** Sort on question score of the RefTest */
  questionScore?: InputMaybe<SortEnumType>;
  /** Sort on total possible question score */
  questionTotal?: InputMaybe<SortEnumType>;
  /** Sort on results were sent for the RefTest */
  resultsSent?: InputMaybe<SortEnumType>;
  /** Sort on start date of the RefTest */
  startedAt?: InputMaybe<SortEnumType>;
  /** Sort on status of the RefTest (e.g., InProgress, Completed, Expired) */
  status?: InputMaybe<SortEnumType>;
};

export enum RefTestStatus {
  Completed = 'COMPLETED',
  Expired = 'EXPIRED',
  InProgress = 'IN_PROGRESS',
  Pending = 'PENDING'
}

export type RefTestStatusOperationFilterInput = {
  eq?: InputMaybe<RefTestStatus>;
  in?: InputMaybe<Array<RefTestStatus>>;
  neq?: InputMaybe<RefTestStatus>;
  nin?: InputMaybe<Array<RefTestStatus>>;
};

/** The title of the RefTest */
export type RefTestTitle = Node & {
  __typename?: 'RefTestTitle';
  /** The RefTest title id */
  id: Scalars['ID']['output'];
  /** RefTest title value */
  value: Scalars['String']['output'];
};

/** Filter RefTest titles based on Value */
export type RefTestTitleFilterInput = {
  and?: InputMaybe<Array<RefTestTitleFilterInput>>;
  /** Filter on RefTest title id */
  id?: InputMaybe<UuidOperationFilterInput>;
  or?: InputMaybe<Array<RefTestTitleFilterInput>>;
  /** Filter on RefTest title value */
  value?: InputMaybe<StringOperationFilterInput>;
};

/** Sort RefTest titles by Value */
export type RefTestTitleSortInput = {
  /** Sort on RefTest title id */
  id?: InputMaybe<SortEnumType>;
  /** Sort on RefTest title value */
  value?: InputMaybe<SortEnumType>;
};

/** A connection to a list of items. */
export type RefTestTitlesConnection = {
  __typename?: 'RefTestTitlesConnection';
  /** A list of edges. */
  edges?: Maybe<Array<RefTestTitlesEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<RefTestTitle>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type RefTestTitlesEdge = {
  __typename?: 'RefTestTitlesEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: RefTestTitle;
};

/** A connection to a list of items. */
export type RefTestsConnection = {
  __typename?: 'RefTestsConnection';
  /** A list of edges. */
  edges?: Maybe<Array<RefTestsEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<RefTest>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type RefTestsEdge = {
  __typename?: 'RefTestsEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: RefTest;
};

export type SaveRefTestProgressError = InvalidRefTestStatusError | RefTestNotFoundError;

export type SaveRefTestProgressInput = {
  currentQuestionIndex: Scalars['Int']['input'];
  language: Scalars['String']['input'];
  selectedAnswerIds: Array<Scalars['String']['input']>;
  token: Scalars['String']['input'];
};

export type SaveRefTestProgressPayload = {
  __typename?: 'SaveRefTestProgressPayload';
  errors?: Maybe<Array<SaveRefTestProgressError>>;
  refTest?: Maybe<RefTest>;
};

export type ScoreConfiguration = {
  __typename?: 'ScoreConfiguration';
  correct: Scalars['Int']['output'];
  inCorrect: Scalars['Int']['output'];
  negativeScore: Scalars['Boolean']['output'];
  notAnswered: Scalars['Int']['output'];
  passingPercentage: Scalars['Int']['output'];
  penalizeGuessingStrategy: Scalars['Boolean']['output'];
};

export type SendInvitationError = {
  __typename?: 'SendInvitationError';
  errorMessage: Scalars['String']['output'];
  refTestId: Scalars['UUID']['output'];
  user?: Maybe<User>;
};

export type SendInvitationsInput = {
  ids: Array<Scalars['ID']['input']>;
};

export type SendInvitationsPayload = {
  __typename?: 'SendInvitationsPayload';
  sendInvitationsResult?: Maybe<SendInvitationsResult>;
};

export type SendInvitationsResult = {
  __typename?: 'SendInvitationsResult';
  errors: Array<SendInvitationError>;
  failed: Scalars['Int']['output'];
  sentRefTests: Array<RefTest>;
  successfullySent: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

export type SendResultError = {
  __typename?: 'SendResultError';
  errorMessage: Scalars['String']['output'];
  refTestId: Scalars['UUID']['output'];
  user?: Maybe<User>;
};

export type SendResultsInput = {
  ids: Array<Scalars['ID']['input']>;
};

export type SendResultsPayload = {
  __typename?: 'SendResultsPayload';
  sendResultsResult?: Maybe<SendResultsResult>;
};

export type SendResultsResult = {
  __typename?: 'SendResultsResult';
  errors: Array<SendResultError>;
  failed: Scalars['Int']['output'];
  sentRefTests: Array<RefTest>;
  successfullySent: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

export enum SortEnumType {
  Asc = 'ASC',
  Desc = 'DESC'
}

export type StartRefTestError = InvalidRefTestStatusError | RefTestExpiredError | RefTestNotFoundError;

export type StartRefTestInput = {
  token: Scalars['String']['input'];
};

export type StartRefTestPayload = {
  __typename?: 'StartRefTestPayload';
  errors?: Maybe<Array<StartRefTestError>>;
  refTest?: Maybe<RefTest>;
};

export type StringOperationFilterInput = {
  and?: InputMaybe<Array<StringOperationFilterInput>>;
  contains?: InputMaybe<Scalars['String']['input']>;
  endsWith?: InputMaybe<Scalars['String']['input']>;
  eq?: InputMaybe<Scalars['String']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['String']['input']>>>;
  ncontains?: InputMaybe<Scalars['String']['input']>;
  nendsWith?: InputMaybe<Scalars['String']['input']>;
  neq?: InputMaybe<Scalars['String']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['String']['input']>>>;
  nstartsWith?: InputMaybe<Scalars['String']['input']>;
  or?: InputMaybe<Array<StringOperationFilterInput>>;
  startsWith?: InputMaybe<Scalars['String']['input']>;
};

export type TitleInput = {
  id?: InputMaybe<Scalars['ID']['input']>;
  name?: InputMaybe<Scalars['String']['input']>;
};

export type User = {
  __typename?: 'User';
  email: Scalars['String']['output'];
  firstName: Scalars['String']['output'];
  lastName: Scalars['String']['output'];
};

export type UserInput = {
  email: Scalars['String']['input'];
  firstName: Scalars['String']['input'];
  lastName: Scalars['String']['input'];
};

export type UuidOperationFilterInput = {
  eq?: InputMaybe<Scalars['UUID']['input']>;
  gt?: InputMaybe<Scalars['UUID']['input']>;
  gte?: InputMaybe<Scalars['UUID']['input']>;
  in?: InputMaybe<Array<InputMaybe<Scalars['UUID']['input']>>>;
  lt?: InputMaybe<Scalars['UUID']['input']>;
  lte?: InputMaybe<Scalars['UUID']['input']>;
  neq?: InputMaybe<Scalars['UUID']['input']>;
  ngt?: InputMaybe<Scalars['UUID']['input']>;
  ngte?: InputMaybe<Scalars['UUID']['input']>;
  nin?: InputMaybe<Array<InputMaybe<Scalars['UUID']['input']>>>;
  nlt?: InputMaybe<Scalars['UUID']['input']>;
  nlte?: InputMaybe<Scalars['UUID']['input']>;
};

export type CompleteRefTestMutationVariables = Exact<{
  input: CompleteRefTestInput;
}>;


export type CompleteRefTestMutation = { __typename?: 'Mutation', completeRefTest: { __typename?: 'CompleteRefTestPayload', refTest?: { __typename?: 'RefTest', id: string, questionScore?: number | null, questionTotal: number, answerScore?: number | null, answerTotal?: number | null, percentage?: number | null } | null } };

export type CreateBulkRefTestsMutationVariables = Exact<{
  input: CreateBulkRefTestsInput;
}>;


export type CreateBulkRefTestsMutation = { __typename?: 'Mutation', createBulkRefTests: { __typename?: 'CreateBulkRefTestsPayload', bulkRefTestsResult?: { __typename?: 'BulkRefTestsResult', totalRequested: number, successfullyCreated: number, failed: number, errors: Array<{ __typename?: 'BulkCreationError', errorMessage: string, user: { __typename?: 'User', firstName: string, lastName: string, email: string } }> } | null } };

export type DeleteRefTestsMutationVariables = Exact<{
  input: DeleteRefTestsInput;
}>;


export type DeleteRefTestsMutation = { __typename?: 'Mutation', deleteRefTests: { __typename?: 'DeleteRefTestsPayload', deleteRefTestsResult?: { __typename?: 'DeleteRefTestsResult', totalRequested: number, successfullyDeleted: number, failed: number, deletedRefTests: Array<{ __typename?: 'RefTest', id: string }>, errors: Array<{ __typename?: 'DeleteRefTestError', refTestId: string, errorMessage: string }> } | null } };

export type GenerateReportMutationVariables = Exact<{
  input: GenerateRefTestsReportInput;
}>;


export type GenerateReportMutation = { __typename?: 'Mutation', generateRefTestsReport: { __typename?: 'GenerateRefTestsReportPayload', generateReportResult?: { __typename?: 'GenerateReportResult', success: boolean, message: string, refTestCount: number } | null } };

export type GetEnabledLanguagesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetEnabledLanguagesQuery = { __typename?: 'Query', enabledLanguages: Array<string> };

export type GetQuestionsByNumberQueryVariables = Exact<{
  numbers: Array<Scalars['String']['input']> | Scalars['String']['input'];
}>;


export type GetQuestionsByNumberQuery = { __typename?: 'Query', questionsByNumber: Array<{ __typename?: 'Question', id: string, number: string, phrase?: Record<string, string> | null }> };

export type GetRefTestByIdQueryVariables = Exact<{
  id: Scalars['ID']['input'];
}>;


export type GetRefTestByIdQuery = { __typename?: 'Query', refTest?: { __typename?: 'RefTest', id: string, name: string, email: string, invitationSent: boolean, resultsSent: boolean, status: RefTestStatus, numberOfQuestions: number, maxTimeInMinutes: number, startedAt?: string | null, completedAt?: string | null, questionScore?: number | null, answerScore?: number | null, questionTotal: number, answerTotal?: number | null, percentage?: number | null, selectedAnswerIds: Array<string>, title?: { __typename?: 'RefTestTitle', id: string, value: string } | null, questions?: Array<{ __typename?: 'Question', id: string, number: string, phrase?: Record<string, string> | null, answers: Array<{ __typename?: 'Answer', id: string, number?: string | null, phrase?: Record<string, string> | null, isCorrect: boolean }> } | null> | null } | null };

export type GetRefTestByTokenQueryVariables = Exact<{
  token: Scalars['String']['input'];
}>;


export type GetRefTestByTokenQuery = { __typename?: 'Query', refTestByToken:
    | { __typename?: 'InvalidRefTestStatusError', message: string }
    | { __typename?: 'RefTest', id: string, name: string, email: string, numberOfQuestions: number, maxTimeInMinutes: number, currentQuestionIndex?: number | null }
    | { __typename?: 'RefTestExpiredError', message: string }
    | { __typename?: 'RefTestNotFoundError', message: string }
   };

export type GetRefTestsAllCountsQueryVariables = Exact<{
  allWhere?: InputMaybe<RefTestFilterInput>;
  pendingWhere?: InputMaybe<RefTestFilterInput>;
  inProgressWhere?: InputMaybe<RefTestFilterInput>;
  completedWhere?: InputMaybe<RefTestFilterInput>;
  expiredWhere?: InputMaybe<RefTestFilterInput>;
}>;


export type GetRefTestsAllCountsQuery = { __typename?: 'Query', all?: { __typename?: 'RefTestsConnection', totalCount: number } | null, pending?: { __typename?: 'RefTestsConnection', totalCount: number } | null, inProgress?: { __typename?: 'RefTestsConnection', totalCount: number } | null, completed?: { __typename?: 'RefTestsConnection', totalCount: number } | null, expired?: { __typename?: 'RefTestsConnection', totalCount: number } | null };

export type GetRefTestsQueryVariables = Exact<{
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<RefTestFilterInput>;
  order?: InputMaybe<Array<RefTestSortInput> | RefTestSortInput>;
}>;


export type GetRefTestsQuery = { __typename?: 'Query', refTests?: { __typename?: 'RefTestsConnection', totalCount: number, edges?: Array<{ __typename?: 'RefTestsEdge', cursor: string, node: { __typename?: 'RefTest', id: string, name: string, email: string, invitationSent: boolean, resultsSent: boolean, status: RefTestStatus, numberOfQuestions: number, maxTimeInMinutes: number, startedAt?: string | null, completedAt?: string | null, questionScore?: number | null, answerScore?: number | null, questionTotal: number, answerTotal?: number | null, percentage?: number | null, title?: { __typename?: 'RefTestTitle', id: string, value: string } | null } }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type GetResultsEmailDelayMinutesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetResultsEmailDelayMinutesQuery = { __typename?: 'Query', resultsEmailDelayMinutes: number };

export type GetScoreConfigurationQueryVariables = Exact<{ [key: string]: never; }>;


export type GetScoreConfigurationQuery = { __typename?: 'Query', scoreConfiguration: { __typename?: 'ScoreConfiguration', passingPercentage: number } };

export type GetRefTestTitlesQueryVariables = Exact<{
  first: Scalars['Int']['input'];
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<RefTestTitleFilterInput>;
  order?: InputMaybe<Array<RefTestTitleSortInput> | RefTestTitleSortInput>;
}>;


export type GetRefTestTitlesQuery = { __typename?: 'Query', refTestTitles?: { __typename?: 'RefTestTitlesConnection', totalCount: number, edges?: Array<{ __typename?: 'RefTestTitlesEdge', cursor: string, node: { __typename?: 'RefTestTitle', id: string, value: string } }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type SaveRefTestProgressMutationVariables = Exact<{
  input: SaveRefTestProgressInput;
}>;


export type SaveRefTestProgressMutation = { __typename?: 'Mutation', saveRefTestProgress: { __typename?: 'SaveRefTestProgressPayload', refTest?: { __typename?: 'RefTest', id: string, currentQuestionIndex?: number | null, selectedAnswerIds: Array<string> } | null } };

export type SearchQuestionsByNumberQueryVariables = Exact<{
  number?: InputMaybe<Scalars['String']['input']>;
}>;


export type SearchQuestionsByNumberQuery = { __typename?: 'Query', searchQuestionsByNumber: Array<{ __typename?: 'Question', id: string, number: string, phrase?: Record<string, string> | null }> };

export type SendRefTestInvitationsMutationVariables = Exact<{
  input: SendInvitationsInput;
}>;


export type SendRefTestInvitationsMutation = { __typename?: 'Mutation', sendInvitations: { __typename?: 'SendInvitationsPayload', sendInvitationsResult?: { __typename?: 'SendInvitationsResult', totalRequested: number, successfullySent: number, failed: number, sentRefTests: Array<{ __typename?: 'RefTest', id: string, invitationSent: boolean }>, errors: Array<{ __typename?: 'SendInvitationError', refTestId: string, errorMessage: string, user?: { __typename?: 'User', firstName: string, lastName: string, email: string } | null }> } | null } };

export type SendRefTestResultsMutationVariables = Exact<{
  input: SendResultsInput;
}>;


export type SendRefTestResultsMutation = { __typename?: 'Mutation', sendResults: { __typename?: 'SendResultsPayload', sendResultsResult?: { __typename?: 'SendResultsResult', totalRequested: number, successfullySent: number, failed: number, sentRefTests: Array<{ __typename?: 'RefTest', id: string, resultsSent: boolean }>, errors: Array<{ __typename?: 'SendResultError', refTestId: string, errorMessage: string, user?: { __typename?: 'User', firstName: string, lastName: string, email: string } | null }> } | null } };

export type StartRefTestMutationVariables = Exact<{
  input: StartRefTestInput;
}>;


export type StartRefTestMutation = { __typename?: 'Mutation', startRefTest: { __typename?: 'StartRefTestPayload', refTest?: { __typename?: 'RefTest', id: string, startedAt?: string | null, maxTimeInMinutes: number, currentQuestionIndex?: number | null, selectedAnswerIds: Array<string>, questions?: Array<{ __typename?: 'Question', id: string, phrase?: Record<string, string> | null, answers: Array<{ __typename?: 'Answer', id: string, phrase?: Record<string, string> | null }> } | null> | null } | null, errors?: Array<
      | { __typename?: 'InvalidRefTestStatusError', message: string }
      | { __typename?: 'RefTestExpiredError', message: string }
      | { __typename?: 'RefTestNotFoundError', message: string }
    > | null } };

export const CompleteRefTestDocument = gql`
    mutation CompleteRefTest($input: CompleteRefTestInput!) {
  completeRefTest(input: $input) {
    refTest {
      id
      questionScore
      questionTotal
      answerScore
      answerTotal
      percentage
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
export const CreateBulkRefTestsDocument = gql`
    mutation CreateBulkRefTests($input: CreateBulkRefTestsInput!) {
  createBulkRefTests(input: $input) {
    bulkRefTestsResult {
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
  export class CreateBulkRefTestsGQL extends Apollo.Mutation<CreateBulkRefTestsMutation, CreateBulkRefTestsMutationVariables> {
    override document = CreateBulkRefTestsDocument;
    
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
export const GenerateReportDocument = gql`
    mutation generateReport($input: GenerateRefTestsReportInput!) {
  generateRefTestsReport(input: $input) {
    generateReportResult {
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
  export class GenerateReportGQL extends Apollo.Mutation<GenerateReportMutation, GenerateReportMutationVariables> {
    override document = GenerateReportDocument;
    
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
    query GetRefTestById($id: ID!) {
  refTest(id: $id) {
    id
    title {
      id
      value
    }
    name
    email
    invitationSent
    resultsSent
    status
    numberOfQuestions
    maxTimeInMinutes
    startedAt
    completedAt
    questionScore
    answerScore
    questionTotal
    answerTotal
    percentage
    selectedAnswerIds
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
export const GetRefTestByTokenDocument = gql`
    query GetRefTestByToken($token: String!) {
  refTestByToken(token: $token) {
    ... on RefTest {
      id
      name
      email
      numberOfQuestions
      maxTimeInMinutes
      currentQuestionIndex
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
export const GetRefTestsAllCountsDocument = gql`
    query GetRefTestsAllCounts($allWhere: RefTestFilterInput, $pendingWhere: RefTestFilterInput, $inProgressWhere: RefTestFilterInput, $completedWhere: RefTestFilterInput, $expiredWhere: RefTestFilterInput) {
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
        status
        numberOfQuestions
        maxTimeInMinutes
        startedAt
        completedAt
        questionScore
        answerScore
        questionTotal
        answerTotal
        percentage
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
export const SaveRefTestProgressDocument = gql`
    mutation SaveRefTestProgress($input: SaveRefTestProgressInput!) {
  saveRefTestProgress(input: $input) {
    refTest {
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
export const StartRefTestDocument = gql`
    mutation StartRefTest($input: StartRefTestInput!) {
  startRefTest(input: $input) {
    refTest {
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