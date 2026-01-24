import { GetRefTestsQuery, RefTestStatus, SortEnumType } from '../../../../../graphql/generated';

export type SortField =
  | 'title'
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'questionScore'
  | 'answerScore'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent'
  | 'resultsSent'
  | 'maxTimeInMinutes';

export interface IRefTestFilter {
  status?: RefTestStatus;
  invitationSent?: boolean;
  resultsSent?: boolean;
  titleValue?: string;
  searchTerm: string;
  sortField: SortField;
  sortDirection: SortEnumType;
  minQuestionScore?: number;
  maxQuestionScore?: number;
  minAnswerScore?: number;
  maxAnswerScore?: number;
  percentageRange?: 'low' | 'medium' | 'high';
  minQuestions?: number;
  maxQuestions?: number;
  minMaxTimeInMinutes?: number;
  maxMaxTimeInMinutes?: number;
  startedAfter?: string;
  startedBefore?: string;
  completedAfter?: string;
  completedBefore?: string;
  pagingInfo: IPagingInfo;
}

export type RefTestNode = NonNullable<
  NonNullable<NonNullable<GetRefTestsQuery['refTests']>['edges']>[number]
>['node'];

export interface IReportResult {
  success: boolean;
  refTestCount: number;
}

export interface IParticipantInfo {
  name: string;
  email: string;
}

export interface IPagingInfo {
  first: number;
  after?: string;
}
