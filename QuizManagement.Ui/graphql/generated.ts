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
  questionIds: Array<Scalars['String']['input']>;
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
  sendInvitations?: Scalars['Boolean']['input'];
  specificQuestionNumbers?: InputMaybe<Array<Scalars['String']['input']>>;
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

export type DeleteAllQuizSessionsPayload = {
  __typename?: 'DeleteAllQuizSessionsPayload';
  boolean?: Maybe<Scalars['Boolean']['output']>;
};

export type DeleteQuizSessionError = QuizSessionNotFoundError;

export type DeleteQuizSessionInput = {
  id: Scalars['ID']['input'];
};

export type DeleteQuizSessionPayload = {
  __typename?: 'DeleteQuizSessionPayload';
  errors?: Maybe<Array<DeleteQuizSessionError>>;
  quizSession?: Maybe<QuizSession>;
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
  deleteAllQuizSessions: DeleteAllQuizSessionsPayload;
  deleteQuizSession: DeleteQuizSessionPayload;
  sendInvitations: SendInvitationsPayload;
  startQuizSession: StartQuizSessionPayload;
};


export type MutationCompleteQuizArgs = {
  input: CompleteQuizInput;
};


export type MutationCreateBulkQuizSessionsArgs = {
  input: CreateBulkQuizSessionsInput;
};


export type MutationDeleteQuizSessionArgs = {
  input: DeleteQuizSessionInput;
};


export type MutationSendInvitationsArgs = {
  input: SendInvitationsInput;
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
  /** Fetches an object given its ID. */
  node?: Maybe<Node>;
  /** Lookup nodes by a list of IDs. */
  nodes: Array<Maybe<Node>>;
  quizSessionById?: Maybe<QuizSession>;
  quizSessionByToken: QuizSessionByTokenResult;
  quizSessions?: Maybe<QuizSessionsConnection>;
  searchQuestionsByNumber: Array<Question>;
};


export type QueryNodeArgs = {
  id: Scalars['ID']['input'];
};


export type QueryNodesArgs = {
  ids: Array<Scalars['ID']['input']>;
};


export type QueryQuizSessionByIdArgs = {
  id: Scalars['UUID']['input'];
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
  /** Completion date and time of the quiz session */
  completedAt?: Maybe<Scalars['DateTime']['output']>;
  /** Creation date and time of the quiz session */
  createdAt: Scalars['DateTime']['output'];
  /** Email of the user who started the quiz */
  email: Scalars['String']['output'];
  /** The quiz session id */
  id: Scalars['ID']['output'];
  /** Maximum time in minutes for the quiz */
  maxTimeInMinutes: Scalars['Int']['output'];
  /** Name of the user who started the quiz (e.g., ) */
  name?: Maybe<Scalars['String']['output']>;
  /** Number of questions in the quiz */
  numberOfQuestions: Scalars['Int']['output'];
  /** Percentage of correct answers */
  percentage?: Maybe<Scalars['Float']['output']>;
  /** Questions for this quiz session */
  questions?: Maybe<Array<Maybe<Question>>>;
  /** Score of the quiz session */
  score?: Maybe<Scalars['Int']['output']>;
  /** Start date and time of the quiz session */
  startedAt?: Maybe<Scalars['DateTime']['output']>;
  /** Status of the quiz session (e.g., InProgress, Completed, Expired) */
  status: QuizSessionStatus;
};

export type QuizSessionByTokenResult = InvalidQuizSessionStatusError | QuizSession | QuizSessionExpiredError | QuizSessionNotFoundError;

export type QuizSessionExpiredError = Error & {
  __typename?: 'QuizSessionExpiredError';
  message: Scalars['String']['output'];
};

/** Filter quiz sessions based on Id, Email or Status */
export type QuizSessionFilterInput = {
  and?: InputMaybe<Array<QuizSessionFilterInput>>;
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
  /** Filter on last name of the user who started the quiz */
  lastName?: InputMaybe<StringOperationFilterInput>;
  /** Filter on maximum time in minutes for the quiz */
  maxTimeInMinutes?: InputMaybe<IntOperationFilterInput>;
  /** Filter on number of questions in the quiz */
  numberOfQuestions?: InputMaybe<IntOperationFilterInput>;
  or?: InputMaybe<Array<QuizSessionFilterInput>>;
  /** Filter on percentage of correct answers */
  percentage?: InputMaybe<FloatOperationFilterInput>;
  /** Filter on score of the quiz session */
  score?: InputMaybe<IntOperationFilterInput>;
  /** Filter on start date of the quiz session */
  startedAt?: InputMaybe<DateTimeOperationFilterInput>;
  /** Filter on status of the quiz session */
  status?: InputMaybe<QuizSessionStatusOperationFilterInput>;
};

export type QuizSessionNotFoundError = Error & {
  __typename?: 'QuizSessionNotFoundError';
  message: Scalars['String']['output'];
};

/** Sort quiz sessions by Id, Email, Status, Creation Date, Start Date, Completion Date, Percentage, Score, Number of Questions and Maximum Time */
export type QuizSessionSortInput = {
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
  /** Sort on last name of the user who started the quiz */
  lastName?: InputMaybe<SortEnumType>;
  /** Sort on maximum time in minutes for the quiz */
  maxTimeInMinutes?: InputMaybe<SortEnumType>;
  /** Sort on number of questions in the quiz */
  numberOfQuestions?: InputMaybe<SortEnumType>;
  /** Sort on percentage of correct answers */
  percentage?: InputMaybe<SortEnumType>;
  /** Sort on score of the quiz session */
  score?: InputMaybe<SortEnumType>;
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

export type CreateBulkQuizSessionsMutationVariables = Exact<{
  input: CreateBulkQuizSessionsInput;
}>;


export type CreateBulkQuizSessionsMutation = { __typename?: 'Mutation', createBulkQuizSessions: { __typename?: 'CreateBulkQuizSessionsPayload', bulkQuizSessionResult?: { __typename?: 'BulkQuizSessionResult', totalRequested: number, successfullyCreated: number, failed: number, createdSessions: Array<{ __typename?: 'QuizSession', id: string, email: string, status: QuizSessionStatus, createdAt: string }>, errors: Array<{ __typename?: 'BulkCreationError', errorMessage: string, user: { __typename?: 'User', firstName: string, lastName: string, email: string } }> } | null } };

export type SearchQuestionsByNumberQueryVariables = Exact<{
  number?: InputMaybe<Scalars['String']['input']>;
}>;


export type SearchQuestionsByNumberQuery = { __typename?: 'Query', searchQuestionsByNumber: Array<{ __typename?: 'Question', id: string, number: string, phrase?: Record<string, string> | null }> };

export const CreateBulkQuizSessionsDocument = gql`
    mutation CreateBulkQuizSessions($input: CreateBulkQuizSessionsInput!) {
  createBulkQuizSessions(input: $input) {
    bulkQuizSessionResult {
      totalRequested
      successfullyCreated
      failed
      createdSessions {
        id
        email
        status
        createdAt
      }
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