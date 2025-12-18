import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { GetQuizSessionsQuery, QuizSessionStatus } from '../../../../../../../graphql/generated';
import { LocalizedDate } from '../../../../../shared/pipes/localized-date';

type SessionNode = NonNullable<
  NonNullable<NonNullable<GetQuizSessionsQuery['quizSessions']>['edges']>[number]
>['node'];

@Component({
  selector: 'tr[app-session-table-row]',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './session-table-row.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block hover:bg-neutral-50 transition-colors',
  },
})
export class SessionTableRow {
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
