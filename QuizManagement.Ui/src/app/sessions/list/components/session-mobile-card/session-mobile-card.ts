import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { GetQuizSessionsQuery, QuizSessionStatus } from '../../../../../../graphql/generated';
import { LocalizedDatePipe } from '../../../../shared/pipes/localized-date.pipe';

type SessionNode = NonNullable<
  NonNullable<NonNullable<GetQuizSessionsQuery['quizSessions']>['edges']>[number]
>['node'];

@Component({
  selector: 'app-session-mobile-card',
  imports: [TranslatePipe, LocalizedDatePipe],
  templateUrl: './session-mobile-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class SessionMobileCard {
  readonly session = input.required<SessionNode>();
  readonly selected = input.required<boolean>();
  readonly visibleColumns = input.required<Set<string>>();

  readonly toggleSelection = output<string>();

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected getStatusClass(status: QuizSessionStatus): string {
    switch (status) {
      case QuizSessionStatus.Completed:
        return 'bg-green-100 text-green-800';
      case QuizSessionStatus.InProgress:
        return 'bg-blue-100 text-blue-800';
      case QuizSessionStatus.Expired:
        return 'bg-red-100 text-red-800';
      case QuizSessionStatus.Pending:
      default:
        return 'bg-yellow-100 text-yellow-800';
    }
  }

  protected onToggleSelection(): void {
    this.toggleSelection.emit(this.session().id);
  }
}
