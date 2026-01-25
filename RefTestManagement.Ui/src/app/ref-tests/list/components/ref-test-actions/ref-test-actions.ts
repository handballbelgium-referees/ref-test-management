import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-ref-test-actions',
  imports: [TranslatePipe],
  templateUrl: './ref-test-actions.html',
  styleUrl: './ref-test-actions.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class RefTestActions {
  protected readonly menuOpen = signal(false);
  readonly selectedCount = input.required<number>();
  readonly hasCompletedRefTestsSelected = input.required<boolean>();
  readonly hasPendingRefTestsSelected = input.required<boolean>();
  readonly hasInProgressOrCompletedRefTestsSelected = input.required<boolean>();
  readonly hasExpiredRefTestsSelected = input.required<boolean>();
  readonly sendingInvitations = input.required<boolean>();
  readonly sendingResults = input.required<boolean>();
  readonly deletingRefTests = input.required<boolean>();
  readonly generatingReport = input.required<boolean>();
  readonly resettingRefTests = input.required<boolean>();
  readonly revivingRefTests = input.required<boolean>();

  readonly sendInvitations = output<void>();
  readonly sendResults = output<void>();
  readonly deleteSelected = output<void>();
  readonly generateReport = output<void>();
  readonly resetSelected = output<void>();
  readonly reviveSelected = output<void>();

  protected toggleMenu(): void {
    this.menuOpen.set(!this.menuOpen());
  }

  protected onSendInvitations(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.sendInvitations.emit();
  }

  protected onSendResults(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.sendResults.emit();
  }

  protected onDeleteSelected(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.deleteSelected.emit();
  }

  protected onGenerateReport(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.generateReport.emit();
  }

  protected onResetSelected(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.resetSelected.emit();
  }

  protected onReviveSelected(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.reviveSelected.emit();
  }
}
