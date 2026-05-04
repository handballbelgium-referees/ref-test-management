import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus, SortEnumType } from '../../../../../../../graphql/generated';
import { LanguageConfig } from '../../../../../services/language-config';
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
  | 'questionScore'
  | 'answerScore'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent'
  | 'resultsSent'
  | 'maxTimeInMinutes';

interface IRefTestFilter {
  status?: RefTestStatus;
  invitationSent?: boolean;
  resultsSent?: boolean;
  titleValue?: string;
  language?: string;
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
}

interface IStatusCounts {
  all: number;
  pending: number;
  inProgress: number;
  completed: number;
  expired: number;
}

@Component({
  selector: 'app-ref-test-filters-card',
  imports: [
    TranslatePipe,
    StatusFilterTabs,
    SortingPanel,
    TitleFilter,
    PerformanceFilters,
    DateRangeFilter,
  ],
  templateUrl: './ref-test-filters-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
  styles: `
    select {
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
      background-position: right 0.5rem center;
      background-repeat: no-repeat;
      background-size: 1.5em 1.5em;
    }
  `,
})
export class RefTestFiltersCard {
  private readonly _languageConfig = inject(LanguageConfig);

  readonly filter = input.required<IRefTestFilter>();
  readonly statusCounts = input.required<IStatusCounts>();

  protected readonly statusFilterChange = output<RefTestStatus | undefined>();
  protected readonly titleFilterChange = output<string | undefined>();
  protected readonly invitationFilterChange = output<boolean | undefined>();
  protected readonly resultsFilterChange = output<boolean | undefined>();
  protected readonly languageFilterChange = output<string | undefined>();

  protected readonly sortingChange = output<{ field: SortField; direction: SortEnumType }>();
  protected readonly performanceFilterChange = output<{
    minQuestionScore?: number;
    maxQuestionScore?: number;
    minAnswerScore?: number;
    maxAnswerScore?: number;
    percentageRange?: 'low' | 'medium' | 'high';
    minQuestions?: number;
    maxQuestions?: number;
    minMaxTimeInMinutes?: number;
    maxMaxTimeInMinutes?: number;
  }>();
  protected readonly dateRangeChange = output<{
    type: 'started' | 'completed';
    after?: string;
    before?: string;
  }>();

  protected readonly filtersExpanded = signal(false);

  protected readonly availableLanguages = toSignal(this._languageConfig.getAvailableLanguages(), {
    initialValue: [],
  });

  protected toggleFilters(): void {
    this.filtersExpanded.update((v) => !v);
  }

  protected onStatusChange(status?: RefTestStatus): void {
    this.statusFilterChange.emit(status);
  }

  protected onSortingChange(sorting: { field: SortField; direction: SortEnumType }): void {
    this.sortingChange.emit(sorting);
  }

  protected onTitleChange(titleValue: string | undefined): void {
    this.titleFilterChange.emit(titleValue);
  }

  protected onInvitationChange(value: string): void {
    this.invitationFilterChange.emit(value === '' ? undefined : value === 'true');
  }

  protected onResultsChange(value: string): void {
    this.resultsFilterChange.emit(value === '' ? undefined : value === 'true');
  }

  protected onLanguageChange(value: string): void {
    this.languageFilterChange.emit(value === '' ? undefined : value);
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
