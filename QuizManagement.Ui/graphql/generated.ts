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

/** IHF quiz answer */
export type Answer = {
  __typename?: 'Answer';
  /** Answer id */
  id: Scalars['String']['output'];
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

export type BulkQuizSessionResult = {
  __typename?: 'BulkQuizSessionResult';
  createdSessions: Array<QuizSession>;
  errors: Array<BulkCreationError>;
  failed: Scalars['Int']['output'];
  successfullyCreated: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

export type CompleteQuizError = EmailError | InvalidQuizSessionStatusError | QuizSessionNotFoundError;

export type CompleteQuizInput = {
  selectedAnswerIds: Array<Scalars['String']['input']>;
  token: Scalars['String']['input'];
};

export type CompleteQuizPayload = {
  __typename?: 'CompleteQuizPayload';
  errors?: Maybe<Array<CompleteQuizError>>;
  quizSession?: Maybe<QuizSession>;
};

export type CreateBulkQuizSessionsInput = {
  maxTimeInMinutes: Scalars['Int']['input'];
  numberOfQuestions: Scalars['Int']['input'];
  randomQuestionsForEachUser: Scalars['Boolean']['input'];
  sendAutomatedInvitations?: Scalars['Boolean']['input'];
  sendAutomatedResults?: Scalars['Boolean']['input'];
  specificQuestionNumbers?: InputMaybe<Array<Scalars['String']['input']>>;
  title: TitleInput;
  users: Array<UserInput>;
};

export type CreateBulkQuizSessionsPayload = {
  __typename?: 'CreateBulkQuizSessionsPayload';
  bulkQuizSessionResult?: Maybe<BulkQuizSessionResult>;
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

export type DeleteQuizSessionError = {
  __typename?: 'DeleteQuizSessionError';
  errorMessage: Scalars['String']['output'];
  quizSessionId: Scalars['UUID']['output'];
};

export type DeleteQuizSessionsInput = {
  ids: Array<Scalars['ID']['input']>;
};

export type DeleteQuizSessionsPayload = {
  __typename?: 'DeleteQuizSessionsPayload';
  deleteQuizSessionsResult?: Maybe<DeleteQuizSessionsResult>;
};

export type DeleteQuizSessionsResult = {
  __typename?: 'DeleteQuizSessionsResult';
  deletedSessions: Array<QuizSession>;
  errors: Array<DeleteQuizSessionError>;
  failed: Scalars['Int']['output'];
  successfullyDeleted: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

export type EmailError = Error & {
  __typename?: 'EmailError';
  message: Scalars['String']['output'];
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

export type GenerateQuizSessionsReportInput = {
  sessionIds: Array<Scalars['ID']['input']>;
};

export type GenerateQuizSessionsReportPayload = {
  __typename?: 'GenerateQuizSessionsReportPayload';
  generateReportResult?: Maybe<GenerateReportResult>;
};

export type GenerateReportResult = {
  __typename?: 'GenerateReportResult';
  message: Scalars['String']['output'];
  sessionCount: Scalars['Int']['output'];
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

export type InvalidQuizSessionStatusError = Error & {
  __typename?: 'InvalidQuizSessionStatusError';
  message: Scalars['String']['output'];
};

export type Mutation = {
  __typename?: 'Mutation';
  completeQuiz: CompleteQuizPayload;
  createBulkQuizSessions: CreateBulkQuizSessionsPayload;
  deleteQuizSessions: DeleteQuizSessionsPayload;
  generateQuizSessionsReport: GenerateQuizSessionsReportPayload;
  sendInvitations: SendInvitationsPayload;
  sendResults: SendResultsPayload;
  startQuizSession: StartQuizSessionPayload;
};


export type MutationCompleteQuizArgs = {
  input: CompleteQuizInput;
};


export type MutationCreateBulkQuizSessionsArgs = {
  input: CreateBulkQuizSessionsInput;
};


export type MutationDeleteQuizSessionsArgs = {
  input: DeleteQuizSessionsInput;
};


export type MutationGenerateQuizSessionsReportArgs = {
  input: GenerateQuizSessionsReportInput;
};


export type MutationSendInvitationsArgs = {
  input: SendInvitationsInput;
};


export type MutationSendResultsArgs = {
  input: SendResultsInput;
};


export type MutationStartQuizSessionArgs = {
  input: StartQuizSessionInput;
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
  quizSessionByToken: QuizSessionByTokenResult;
  quizSessions?: Maybe<QuizSessionsConnection>;
  quizTitles?: Maybe<QuizTitlesConnection>;
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


export type QueryQuizSessionByTokenArgs = {
  token: Scalars['String']['input'];
};


export type QueryQuizSessionsArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<QuizSessionSortInput>>;
  where?: InputMaybe<QuizSessionFilterInput>;
};


export type QueryQuizTitlesArgs = {
  after?: InputMaybe<Scalars['String']['input']>;
  before?: InputMaybe<Scalars['String']['input']>;
  first?: InputMaybe<Scalars['Int']['input']>;
  last?: InputMaybe<Scalars['Int']['input']>;
  order?: InputMaybe<Array<QuizTitleSortInput>>;
  where?: InputMaybe<QuizTitleFilterInput>;
};


export type QuerySearchQuestionsByNumberArgs = {
  number?: InputMaybe<Scalars['String']['input']>;
};

/** IHF quiz question */
export type Question = {
  __typename?: 'Question';
  /** Answers for this question */
  answers: Array<Answer>;
  /** Question id */
  id: Scalars['String']['output'];
  /** Question number */
  number: Scalars['String']['output'];
  /** Translations of the question phrase */
  phrase?: Maybe<Scalars['JSON']['output']>;
};

/** IHF quiz session */
export type QuizSession = Node & {
  __typename?: 'QuizSession';
  /** Score based on individual answers */
  answerScore?: Maybe<Scalars['Int']['output']>;
  /** Total possible answer score */
  answerTotal?: Maybe<Scalars['Int']['output']>;
  /** Completion date and time of the quiz session */
  completedAt?: Maybe<Scalars['DateTime']['output']>;
  /** Creation date and time of the quiz session */
  createdAt: Scalars['DateTime']['output'];
  /** Email of the user who started the quiz */
  email: Scalars['String']['output'];
  /** The quiz session id */
  id: Scalars['ID']['output'];
  /** Indication of invitation was sent */
  invitationSent: Scalars['Boolean']['output'];
  /** Maximum time in minutes for the quiz */
  maxTimeInMinutes: Scalars['Int']['output'];
  /** Name of the user who started the quiz (e.g., ) */
  name?: Maybe<Scalars['String']['output']>;
  /** Number of questions in the quiz */
  numberOfQuestions: Scalars['Int']['output'];
  /** Percentage of correct answers */
  percentage?: Maybe<Scalars['Float']['output']>;
  /** Score based on fully correct questions */
  questionScore?: Maybe<Scalars['Int']['output']>;
  /** Total possible question score */
  questionTotal: Scalars['Int']['output'];
  /** Questions for this quiz session */
  questions?: Maybe<Array<Maybe<Question>>>;
  /** Indication of results were sent */
  resultsSent: Scalars['Boolean']['output'];
  /** Start date and time of the quiz session */
  startedAt?: Maybe<Scalars['DateTime']['output']>;
  /** Status of the quiz session (e.g., InProgress, Completed, Expired) */
  status: QuizSessionStatus;
  /** Title of the quiz */
  title?: Maybe<QuizTitle>;
  /** List of answer IDs that were answered incorrectly */
  wrongAnswerIds: Array<Scalars['String']['output']>;
  /** List of question IDs that were answered incorrectly */
  wrongQuestionIds: Array<Scalars['String']['output']>;
};


/** IHF quiz session */
export type QuizSessionQuestionsArgs = {
  includeIsCorrect?: InputMaybe<Scalars['Boolean']['input']>;
  includeNumber?: InputMaybe<Scalars['Boolean']['input']>;
  randomAnswerOrder?: InputMaybe<Scalars['Boolean']['input']>;
};

export type QuizSessionByTokenResult = InvalidQuizSessionStatusError | QuizSession | QuizSessionExpiredError | QuizSessionNotFoundError;

export type QuizSessionExpiredError = Error & {
  __typename?: 'QuizSessionExpiredError';
  message: Scalars['String']['output'];
};

/** Filter quiz sessions based on Id, Email or Status */
export type QuizSessionFilterInput = {
  and?: InputMaybe<Array<QuizSessionFilterInput>>;
  /** Filter on answer score of the quiz session */
  answerScore?: InputMaybe<IntOperationFilterInput>;
  /** Filter on total possible answer score */
  answerTotal?: InputMaybe<IntOperationFilterInput>;
  /** Filter on completion date of the quiz session */
  completedAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on creation date of the quiz session */
  createdAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on email of the user who started the quiz */
  email?: InputMaybe<StringOperationFilterInput>;
  /** Filter on first name of the user who started the quiz */
  firstName?: InputMaybe<StringOperationFilterInput>;
  /** Filter on quiz session id */
  id?: InputMaybe<UuidOperationFilterInput>;
  /** Filter on invitation was sent */
  invitationSent?: InputMaybe<BooleanOperationFilterInput>;
  /** Filter on last name of the user who started the quiz */
  lastName?: InputMaybe<StringOperationFilterInput>;
  /** Filter on maximum time in minutes for the quiz */
  maxTimeInMinutes?: InputMaybe<IntOperationFilterInput>;
  /** Filter on number of questions in the quiz */
  numberOfQuestions?: InputMaybe<IntOperationFilterInput>;
  or?: InputMaybe<Array<QuizSessionFilterInput>>;
  /** Filter on percentage of correct answers */
  percentage?: InputMaybe<FloatOperationFilterInput>;
  /** Filter on question score of the quiz session */
  questionScore?: InputMaybe<IntOperationFilterInput>;
  /** Filter on total possible question score */
  questionTotal?: InputMaybe<IntOperationFilterInput>;
  /** Filter on results were sent */
  resultsSent?: InputMaybe<BooleanOperationFilterInput>;
  /** Filter on start date of the quiz session */
  startedAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on status of the quiz session */
  status?: InputMaybe<QuizSessionStatusOperationFilterInput>;
  /** Filter on quiz title */
  title?: InputMaybe<QuizTitleFilterInput>;
};

export type QuizSessionNotFoundError = Error & {
  __typename?: 'QuizSessionNotFoundError';
  message: Scalars['String']['output'];
};

/** Sort quiz sessions by Id, Email, Status, Creation Date, Start Date, Completion Date, Percentage, QuestionScore, Number of Questions and Maximum Time */
export type QuizSessionSortInput = {
  /** Sort on answer score of the quiz session */
  answerScore?: InputMaybe<SortEnumType>;
  /** Sort on total possible answer score */
  answerTotal?: InputMaybe<SortEnumType>;
  /** Sort on completion date of the quiz session */
  completedAt?: InputMaybe<SortEnumType>;
  /** Sort on creation date of the quiz session */
  createdAt?: InputMaybe<SortEnumType>;
  /** Sort on email of the user who started the quiz */
  email?: InputMaybe<SortEnumType>;
  /** Sort on first name of the user who started the quiz */
  firstName?: InputMaybe<SortEnumType>;
  /** Sort on quiz session id */
  id?: InputMaybe<SortEnumType>;
  /** Sort on invitation was sent */
  invitationSent?: InputMaybe<SortEnumType>;
  /** Sort on last name of the user who started the quiz */
  lastName?: InputMaybe<SortEnumType>;
  /** Sort on maximum time in minutes for the quiz */
  maxTimeInMinutes?: InputMaybe<SortEnumType>;
  /** Sort on number of questions in the quiz */
  numberOfQuestions?: InputMaybe<SortEnumType>;
  /** Sort on percentage of correct answers */
  percentage?: InputMaybe<SortEnumType>;
  /** Sort on question score of the quiz session */
  questionScore?: InputMaybe<SortEnumType>;
  /** Sort on total possible question score */
  questionTotal?: InputMaybe<SortEnumType>;
  /** Sort on results were sent */
  resultsSent?: InputMaybe<SortEnumType>;
  /** Sort on start date of the quiz session */
  startedAt?: InputMaybe<SortEnumType>;
  /** Sort on status of the quiz session */
  status?: InputMaybe<SortEnumType>;
};

export enum QuizSessionStatus {
  Completed = 'COMPLETED',
  Expired = 'EXPIRED',
  InProgress = 'IN_PROGRESS',
  Pending = 'PENDING'
}

export type QuizSessionStatusOperationFilterInput = {
  eq?: InputMaybe<QuizSessionStatus>;
  in?: InputMaybe<Array<QuizSessionStatus>>;
  neq?: InputMaybe<QuizSessionStatus>;
  nin?: InputMaybe<Array<QuizSessionStatus>>;
};

/** A connection to a list of items. */
export type QuizSessionsConnection = {
  __typename?: 'QuizSessionsConnection';
  /** A list of edges. */
  edges?: Maybe<Array<QuizSessionsEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<QuizSession>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type QuizSessionsEdge = {
  __typename?: 'QuizSessionsEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: QuizSession;
};

/** The title of the quiz session */
export type QuizTitle = Node & {
  __typename?: 'QuizTitle';
  /** The quiz title id */
  id: Scalars['ID']['output'];
  /** Quiz title value */
  value: Scalars['String']['output'];
};

export type QuizTitleFilterInput = {
  and?: InputMaybe<Array<QuizTitleFilterInput>>;
  id?: InputMaybe<UuidOperationFilterInput>;
  or?: InputMaybe<Array<QuizTitleFilterInput>>;
  value?: InputMaybe<StringOperationFilterInput>;
};

export type QuizTitleSortInput = {
  id?: InputMaybe<SortEnumType>;
  value?: InputMaybe<SortEnumType>;
};

/** A connection to a list of items. */
export type QuizTitlesConnection = {
  __typename?: 'QuizTitlesConnection';
  /** A list of edges. */
  edges?: Maybe<Array<QuizTitlesEdge>>;
  /** A flattened list of the nodes. */
  nodes?: Maybe<Array<QuizTitle>>;
  /** Information to aid in pagination. */
  pageInfo: PageInfo;
  /** Identifies the total count of items in the connection. */
  totalCount: Scalars['Int']['output'];
};

/** An edge in a connection. */
export type QuizTitlesEdge = {
  __typename?: 'QuizTitlesEdge';
  /** A cursor for use in pagination. */
  cursor: Scalars['String']['output'];
  /** The item at the end of the edge. */
  node: QuizTitle;
};

export type ScoreConfiguration = {
  __typename?: 'ScoreConfiguration';
  passingPercentage: Scalars['Int']['output'];
};

export type SendInvitationError = {
  __typename?: 'SendInvitationError';
  errorMessage: Scalars['String']['output'];
  quizSessionId: Scalars['UUID']['output'];
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
  sentSessions: Array<QuizSession>;
  successfullySent: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
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
  errors: Array<SendInvitationError>;
  failed: Scalars['Int']['output'];
  sentSessions: Array<QuizSession>;
  successfullySent: Scalars['Int']['output'];
  totalRequested: Scalars['Int']['output'];
};

export enum SortEnumType {
  Asc = 'ASC',
  Desc = 'DESC'
}

export type StartQuizSessionError = InvalidQuizSessionStatusError | QuizSessionExpiredError | QuizSessionNotFoundError;

export type StartQuizSessionInput = {
  token: Scalars['String']['input'];
};

export type StartQuizSessionPayload = {
  __typename?: 'StartQuizSessionPayload';
  errors?: Maybe<Array<StartQuizSessionError>>;
  quizSession?: Maybe<QuizSession>;
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

export type CompleteQuizSessionMutationVariables = Exact<{
  input: CompleteQuizInput;
}>;


export type CompleteQuizSessionMutation = { __typename?: 'Mutation', completeQuiz: { __typename?: 'CompleteQuizPayload', quizSession?: { __typename?: 'QuizSession', id: string, questionScore?: number | null, questionTotal: number, answerScore?: number | null, answerTotal?: number | null, percentage?: number | null } | null } };

export type CreateBulkQuizSessionsMutationVariables = Exact<{
  input: CreateBulkQuizSessionsInput;
}>;


export type CreateBulkQuizSessionsMutation = { __typename?: 'Mutation', createBulkQuizSessions: { __typename?: 'CreateBulkQuizSessionsPayload', bulkQuizSessionResult?: { __typename?: 'BulkQuizSessionResult', totalRequested: number, successfullyCreated: number, failed: number, errors: Array<{ __typename?: 'BulkCreationError', errorMessage: string, user: { __typename?: 'User', firstName: string, lastName: string, email: string } }> } | null } };

export type DeleteQuizSessionsMutationVariables = Exact<{
  input: DeleteQuizSessionsInput;
}>;


export type DeleteQuizSessionsMutation = { __typename?: 'Mutation', deleteQuizSessions: { __typename?: 'DeleteQuizSessionsPayload', deleteQuizSessionsResult?: { __typename?: 'DeleteQuizSessionsResult', totalRequested: number, successfullyDeleted: number, failed: number, deletedSessions: Array<{ __typename?: 'QuizSession', id: string }>, errors: Array<{ __typename?: 'DeleteQuizSessionError', quizSessionId: string, errorMessage: string }> } | null } };

export type GenerateReportMutationVariables = Exact<{
  input: GenerateQuizSessionsReportInput;
}>;


export type GenerateReportMutation = { __typename?: 'Mutation', generateQuizSessionsReport: { __typename?: 'GenerateQuizSessionsReportPayload', generateReportResult?: { __typename?: 'GenerateReportResult', success: boolean, message: string, sessionCount: number } | null } };

export type GetEnabledLanguagesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetEnabledLanguagesQuery = { __typename?: 'Query', enabledLanguages: Array<string> };

export type GetResultsEmailDelayMinutesQueryVariables = Exact<{ [key: string]: never; }>;


export type GetResultsEmailDelayMinutesQuery = { __typename?: 'Query', resultsEmailDelayMinutes: number };

export type GetScoreConfigurationQueryVariables = Exact<{ [key: string]: never; }>;


export type GetScoreConfigurationQuery = { __typename?: 'Query', scoreConfiguration: { __typename?: 'ScoreConfiguration', passingPercentage: number } };

export type GetQuizSessionByTokenQueryVariables = Exact<{
  token: Scalars['String']['input'];
}>;


export type GetQuizSessionByTokenQuery = { __typename?: 'Query', quizSessionByToken:
    | { __typename?: 'InvalidQuizSessionStatusError', message: string }
    | { __typename?: 'QuizSession', id: string, name?: string | null, email: string, numberOfQuestions: number, maxTimeInMinutes: number }
    | { __typename?: 'QuizSessionExpiredError', message: string }
    | { __typename?: 'QuizSessionNotFoundError', message: string }
   };

export type GetQuizSessionsCountQueryVariables = Exact<{
  where?: InputMaybe<QuizSessionFilterInput>;
}>;


export type GetQuizSessionsCountQuery = { __typename?: 'Query', quizSessions?: { __typename?: 'QuizSessionsConnection', totalCount: number } | null };

export type GetQuizSessionsQueryVariables = Exact<{
  first?: InputMaybe<Scalars['Int']['input']>;
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<QuizSessionFilterInput>;
  order?: InputMaybe<Array<QuizSessionSortInput> | QuizSessionSortInput>;
}>;


export type GetQuizSessionsQuery = { __typename?: 'Query', quizSessions?: { __typename?: 'QuizSessionsConnection', totalCount: number, edges?: Array<{ __typename?: 'QuizSessionsEdge', cursor: string, node: { __typename?: 'QuizSession', id: string, name?: string | null, email: string, invitationSent: boolean, resultsSent: boolean, status: QuizSessionStatus, numberOfQuestions: number, maxTimeInMinutes: number, startedAt?: string | null, completedAt?: string | null, questionScore?: number | null, answerScore?: number | null, questionTotal: number, answerTotal?: number | null, percentage?: number | null, title?: { __typename?: 'QuizTitle', id: string, value: string } | null } }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type GetQuizTitlesQueryVariables = Exact<{
  first: Scalars['Int']['input'];
  after?: InputMaybe<Scalars['String']['input']>;
  where?: InputMaybe<QuizTitleFilterInput>;
  order?: InputMaybe<Array<QuizTitleSortInput> | QuizTitleSortInput>;
}>;


export type GetQuizTitlesQuery = { __typename?: 'Query', quizTitles?: { __typename?: 'QuizTitlesConnection', totalCount: number, edges?: Array<{ __typename?: 'QuizTitlesEdge', cursor: string, node: { __typename?: 'QuizTitle', id: string, value: string } }> | null, pageInfo: { __typename?: 'PageInfo', hasNextPage: boolean, endCursor?: string | null } } | null };

export type SearchQuestionsByNumberQueryVariables = Exact<{
  number?: InputMaybe<Scalars['String']['input']>;
}>;


export type SearchQuestionsByNumberQuery = { __typename?: 'Query', searchQuestionsByNumber: Array<{ __typename?: 'Question', id: string, number: string, phrase?: Record<string, string> | null }> };

export type SendQuizInvitationsMutationVariables = Exact<{
  input: SendInvitationsInput;
}>;


export type SendQuizInvitationsMutation = { __typename?: 'Mutation', sendInvitations: { __typename?: 'SendInvitationsPayload', sendInvitationsResult?: { __typename?: 'SendInvitationsResult', totalRequested: number, successfullySent: number, failed: number, sentSessions: Array<{ __typename?: 'QuizSession', id: string, invitationSent: boolean }>, errors: Array<{ __typename?: 'SendInvitationError', quizSessionId: string, errorMessage: string, user?: { __typename?: 'User', firstName: string, lastName: string, email: string } | null }> } | null } };

export type SendQuizResultsMutationVariables = Exact<{
  input: SendResultsInput;
}>;


export type SendQuizResultsMutation = { __typename?: 'Mutation', sendResults: { __typename?: 'SendResultsPayload', sendResultsResult?: { __typename?: 'SendResultsResult', totalRequested: number, successfullySent: number, failed: number, sentSessions: Array<{ __typename?: 'QuizSession', id: string, resultsSent: boolean }>, errors: Array<{ __typename?: 'SendInvitationError', quizSessionId: string, errorMessage: string, user?: { __typename?: 'User', firstName: string, lastName: string, email: string } | null }> } | null } };

export type StartQuizSessionMutationVariables = Exact<{
  input: StartQuizSessionInput;
}>;


export type StartQuizSessionMutation = { __typename?: 'Mutation', startQuizSession: { __typename?: 'StartQuizSessionPayload', quizSession?: { __typename?: 'QuizSession', id: string, startedAt?: string | null, maxTimeInMinutes: number, questions?: Array<{ __typename?: 'Question', id: string, phrase?: Record<string, string> | null, answers: Array<{ __typename?: 'Answer', id: string, phrase?: Record<string, string> | null }> } | null> | null } | null, errors?: Array<
      | { __typename?: 'InvalidQuizSessionStatusError', message: string }
      | { __typename?: 'QuizSessionExpiredError', message: string }
      | { __typename?: 'QuizSessionNotFoundError', message: string }
    > | null } };

export const CompleteQuizSessionDocument = gql`
    mutation CompleteQuizSession($input: CompleteQuizInput!) {
  completeQuiz(input: $input) {
    quizSession {
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
  export class CompleteQuizSessionGQL extends Apollo.Mutation<CompleteQuizSessionMutation, CompleteQuizSessionMutationVariables> {
    override document = CompleteQuizSessionDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const CreateBulkQuizSessionsDocument = gql`
    mutation CreateBulkQuizSessions($input: CreateBulkQuizSessionsInput!) {
  createBulkQuizSessions(input: $input) {
    bulkQuizSessionResult {
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
  export class CreateBulkQuizSessionsGQL extends Apollo.Mutation<CreateBulkQuizSessionsMutation, CreateBulkQuizSessionsMutationVariables> {
    override document = CreateBulkQuizSessionsDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const DeleteQuizSessionsDocument = gql`
    mutation DeleteQuizSessions($input: DeleteQuizSessionsInput!) {
  deleteQuizSessions(input: $input) {
    deleteQuizSessionsResult {
      totalRequested
      successfullyDeleted
      failed
      deletedSessions {
        id
      }
      errors {
        quizSessionId
        errorMessage
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class DeleteQuizSessionsGQL extends Apollo.Mutation<DeleteQuizSessionsMutation, DeleteQuizSessionsMutationVariables> {
    override document = DeleteQuizSessionsDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GenerateReportDocument = gql`
    mutation generateReport($input: GenerateQuizSessionsReportInput!) {
  generateQuizSessionsReport(input: $input) {
    generateReportResult {
      success
      message
      sessionCount
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
export const GetQuizSessionByTokenDocument = gql`
    query GetQuizSessionByToken($token: String!) {
  quizSessionByToken(token: $token) {
    ... on QuizSession {
      id
      name
      email
      numberOfQuestions
      maxTimeInMinutes
    }
    ... on QuizSessionNotFoundError {
      message
    }
    ... on QuizSessionExpiredError {
      message
    }
    ... on InvalidQuizSessionStatusError {
      message
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetQuizSessionByTokenGQL extends Apollo.Query<GetQuizSessionByTokenQuery, GetQuizSessionByTokenQueryVariables> {
    override document = GetQuizSessionByTokenDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetQuizSessionsCountDocument = gql`
    query GetQuizSessionsCount($where: QuizSessionFilterInput) {
  quizSessions(where: $where) {
    totalCount
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class GetQuizSessionsCountGQL extends Apollo.Query<GetQuizSessionsCountQuery, GetQuizSessionsCountQueryVariables> {
    override document = GetQuizSessionsCountDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetQuizSessionsDocument = gql`
    query GetQuizSessions($first: Int, $after: String, $where: QuizSessionFilterInput, $order: [QuizSessionSortInput!]) {
  quizSessions(first: $first, after: $after, where: $where, order: $order) {
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
  export class GetQuizSessionsGQL extends Apollo.Query<GetQuizSessionsQuery, GetQuizSessionsQueryVariables> {
    override document = GetQuizSessionsDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const GetQuizTitlesDocument = gql`
    query GetQuizTitles($first: Int!, $after: String, $where: QuizTitleFilterInput, $order: [QuizTitleSortInput!]) {
  quizTitles(first: $first, after: $after, where: $where, order: $order) {
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
  export class GetQuizTitlesGQL extends Apollo.Query<GetQuizTitlesQuery, GetQuizTitlesQueryVariables> {
    override document = GetQuizTitlesDocument;
    
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
export const SendQuizInvitationsDocument = gql`
    mutation SendQuizInvitations($input: SendInvitationsInput!) {
  sendInvitations(input: $input) {
    sendInvitationsResult {
      totalRequested
      successfullySent
      failed
      sentSessions {
        id
        invitationSent
      }
      errors {
        quizSessionId
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
  export class SendQuizInvitationsGQL extends Apollo.Mutation<SendQuizInvitationsMutation, SendQuizInvitationsMutationVariables> {
    override document = SendQuizInvitationsDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const SendQuizResultsDocument = gql`
    mutation SendQuizResults($input: SendResultsInput!) {
  sendResults(input: $input) {
    sendResultsResult {
      totalRequested
      successfullySent
      failed
      sentSessions {
        id
        resultsSent
      }
      errors {
        quizSessionId
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
  export class SendQuizResultsGQL extends Apollo.Mutation<SendQuizResultsMutation, SendQuizResultsMutationVariables> {
    override document = SendQuizResultsDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }
export const StartQuizSessionDocument = gql`
    mutation StartQuizSession($input: StartQuizSessionInput!) {
  startQuizSession(input: $input) {
    quizSession {
      id
      startedAt
      maxTimeInMinutes
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
      ... on QuizSessionNotFoundError {
        message
      }
      ... on QuizSessionExpiredError {
        message
      }
      ... on InvalidQuizSessionStatusError {
        message
      }
    }
  }
}
    `;

  @Injectable({
    providedIn: 'root'
  })
  export class StartQuizSessionGQL extends Apollo.Mutation<StartQuizSessionMutation, StartQuizSessionMutationVariables> {
    override document = StartQuizSessionDocument;
    
    constructor(apollo: Apollo.Apollo) {
      super(apollo);
    }
  }