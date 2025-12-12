import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
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
})
export class SessionFiltersCard {
  readonly filter = input.required<SessionFilter>();
  readonly statusCounts = input.required<StatusCounts>();

  readonly statusFilterChange = output<QuizSessionStatus | undefined>();
  readonly invitationFilterChange = output<boolean | undefined>();
  readonly sortingChange = output<{ field: SortField; direction: SortEnumType }>();

  protected readonly QuizSessionStatus = QuizSessionStatus;
  protected readonly SortEnumType = SortEnumType;

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
}
