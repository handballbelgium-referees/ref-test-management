import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { QuizSessionStatus, SortEnumType } from '../../../../../../graphql/generated';

type SortField =
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'score'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent';

interface SessionFilter {
  status?: QuizSessionStatus;
  invitationSent?: boolean;
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

interface StatusCounts {
  all: number;
  pending: number;
  inProgress: number;
  completed: number;
  expired: number;
}

@Component({
  selector: 'app-session-filters-card',
  imports: [TranslatePipe],
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
  readonly filter = input.required<SessionFilter>();
  readonly statusCounts = input.required<StatusCounts>();

  readonly statusFilterChange = output<QuizSessionStatus | undefined>();
  readonly invitationFilterChange = output<boolean | undefined>();
  readonly sortingChange = output<{ field: SortField; direction: SortEnumType }>();
  readonly scoreRangeChange = output<{ min?: number; max?: number }>();
  readonly percentageRangeChange = output<'low' | 'medium' | 'high' | undefined>();
  readonly questionsRangeChange = output<{ min?: number; max?: number }>();
  readonly dateRangeChange = output<{
    type: 'started' | 'completed';
    after?: string;
    before?: string;
  }>();

  protected readonly QuizSessionStatus = QuizSessionStatus;
  protected readonly SortEnumType = SortEnumType;

  protected readonly filtersExpanded = signal(false);
  protected readonly sortingExpanded = signal(false);

  protected toggleFilters(): void {
    this.filtersExpanded.update((v) => !v);
  }

  protected toggleSorting(): void {
    this.sortingExpanded.update((v) => !v);
  }

  protected onStatusChange(status?: QuizSessionStatus): void {
    this.statusFilterChange.emit(status);
  }

  protected onInvitationChange(value: string): void {
    this.invitationFilterChange.emit(value === '' ? undefined : value === 'true');
  }

  protected onSortFieldChange(field: string): void {
    this.sortingChange.emit({
      field: field as SortField,
      direction: this.filter().sortDirection,
    });
  }

  protected onSortDirectionChange(direction: string): void {
    this.sortingChange.emit({
      field: this.filter().sortField,
      direction: direction as SortEnumType,
    });
  }

  protected onMinScoreChange(value: string): void {
    const min = value ? Number(value) : undefined;
    this.scoreRangeChange.emit({ min, max: this.filter().maxScore });
  }

  protected onMaxScoreChange(value: string): void {
    const max = value ? Number(value) : undefined;
    this.scoreRangeChange.emit({ min: this.filter().minScore, max });
  }

  protected onPercentageRangeChange(value: string): void {
    this.percentageRangeChange.emit(
      value === '' ? undefined : (value as 'low' | 'medium' | 'high')
    );
  }

  protected onMinQuestionsChange(value: string): void {
    const min = value ? Number(value) : undefined;
    this.questionsRangeChange.emit({ min, max: this.filter().maxQuestions });
  }

  protected onMaxQuestionsChange(value: string): void {
    const max = value ? Number(value) : undefined;
    this.questionsRangeChange.emit({ min: this.filter().minQuestions, max });
  }

  protected onStartedAfterChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'started',
      after: value || undefined,
      before: this.filter().startedBefore,
    });
  }

  protected onStartedBeforeChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'started',
      after: this.filter().startedAfter,
      before: value || undefined,
    });
  }

  protected onCompletedAfterChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'completed',
      after: value || undefined,
      before: this.filter().completedBefore,
    });
  }

  protected onCompletedBeforeChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'completed',
      after: this.filter().completedAfter,
      before: value || undefined,
    });
  }
}
