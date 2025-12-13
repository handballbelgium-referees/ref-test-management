import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-session-bulk-actions',
  imports: [TranslatePipe],
  templateUrl: './session-bulk-actions.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionBulkActions {
  readonly selectedCount = input.required<number>();
  readonly hasCompletedSessionsSelected = input.required<boolean>();
  readonly hasPendingSessionsSelected = input.required<boolean>();
  readonly sendingInvitations = input.required<boolean>();
  readonly sendingResults = input.required<boolean>();

  readonly sendInvitations = output<void>();
  readonly sendResults = output<void>();
  readonly deleteSelected = output<void>();

  protected onSendInvitations(event: Event): void {
    event.stopPropagation();
    this.sendInvitations.emit();
  }

  protected onSendResults(event: Event): void {
    event.stopPropagation();
    this.sendResults.emit();
  }

  protected onDeleteSelected(event: Event): void {
    event.stopPropagation();
    this.deleteSelected.emit();
  }
}
