import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { QuizSessionStatus, SortEnumType } from '../../../../../../../graphql/generated';
import { DateRangeFilter } from '../date-range-filter/date-range-filter';
import { PerformanceFilters } from '../performance-filters/performance-filters';
import { SortingPanel } from '../sorting-panel/sorting-panel';
import { StatusFilterTabs } from '../status-filter-tabs/status-filter-tabs';
import { TitleFilter } from '../title-filter/title-filter';

type SortField =
  | 'title'
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'score'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent'
  | 'resultsSent';

interface ISessionFilter {
  status?: QuizSessionStatus;
  invitationSent?: boolean;
  resultsSent?: boolean;
  titleId?: string;
  searchTerm: string;
  sortField: SortField;
  sortDirection: SortEnumType;
  minScore?: number;
  maxScore?: number;
  percentageRange?: 'low' | 'medium' | 'high';
  minQuestions?: number;
  maxQuestions?: number;
  startedAfter?: string;
  startedBefore?: string;
  completedAfter?: string;
  completedBefore?: string;
}

interface IStatusCounts {
  all: number;
  pending: number;
  inProgress: number;
  completed: number;
  expired: number;
}

@Component({
  selector: 'app-session-filters-card',
  imports: [
    TranslatePipe,
    StatusFilterTabs,
    SortingPanel,
    TitleFilter,
    PerformanceFilters,
    DateRangeFilter,
  ],
  templateUrl: './session-filters-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    select {
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
      background-position: right 0.5rem center;
      background-repeat: no-repeat;
      background-size: 1.5em 1.5em;
    }
  `,
})
export class SessionFiltersCard {
  readonly filter = input.required<ISessionFilter>();
  readonly statusCounts = input.required<IStatusCounts>();

  readonly statusFilterChange = output<QuizSessionStatus | undefined>();
  readonly titleFilterChange = output<string | undefined>();
  readonly invitationFilterChange = output<boolean | undefined>();
  readonly resultsFilterChange = output<boolean | undefined>();

  readonly sortingChange = output<{ field: SortField; direction: SortEnumType }>();
  readonly performanceFilterChange = output<{
    minScore?: number;
    maxScore?: number;
    percentageRange?: 'low' | 'medium' | 'high';
    minQuestions?: number;
    maxQuestions?: number;
  }>();
  readonly dateRangeChange = output<{
    type: 'started' | 'completed';
    after?: string;
    before?: string;
  }>();

  protected readonly QuizSessionStatus = QuizSessionStatus;
  protected readonly SortEnumType = SortEnumType;

  protected readonly filtersExpanded = signal(false);

  protected toggleFilters(): void {
    this.filtersExpanded.update((v) => !v);
  }

  protected onStatusChange(status?: QuizSessionStatus): void {
    this.statusFilterChange.emit(status);
  }

  protected onSortingChange(sorting: { field: SortField; direction: SortEnumType }): void {
    this.sortingChange.emit(sorting);
  }

  protected onTitleChange(titleId: string | undefined): void {
    this.titleFilterChange.emit(titleId);
  }

  protected onInvitationChange(value: string): void {
    this.invitationFilterChange.emit(value === '' ? undefined : value === 'true');
  }

  protected onResultsChange(value: string): void {
    this.resultsFilterChange.emit(value === '' ? undefined : value === 'true');
  }

  protected onPerformanceChange(changes: {
    minScore?: number;
    maxScore?: number;
    percentageRange?: 'low' | 'medium' | 'high' | '';
    minQuestions?: number;
    maxQuestions?: number;
  }): void {
    this.performanceFilterChange.emit({
      ...this.filter(),
      ...changes,
      percentageRange: changes.percentageRange === '' ? undefined : changes.percentageRange,
    });
  }

  protected onStartedDateChange(changes: { after?: string; before?: string }): void {
    this.dateRangeChange.emit({
      type: 'started',
      after: changes.after,
      before: changes.before,
    });
  }

  protected onCompletedDateChange(changes: { after?: string; before?: string }): void {
    this.dateRangeChange.emit({
      type: 'completed',
      after: changes.after,
      before: changes.before,
    });
  }
}
