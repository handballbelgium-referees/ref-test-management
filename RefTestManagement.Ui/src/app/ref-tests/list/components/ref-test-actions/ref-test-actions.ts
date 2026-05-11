import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { HasPermission } from '../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../auth/models/permissions';

@Component({
  selector: 'app-ref-test-actions',
  imports: [TranslatePipe, HasPermission],
  templateUrl: './ref-test-actions.html',
  styleUrl: './ref-test-actions.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class RefTestActions {
  protected readonly menuOpen = signal(false);
  protected readonly Permissions = Permissions;

  readonly selectedCount = input.required<number>();
  readonly hasCompletedRefTestsSelected = input.required<boolean>();
  readonly hasPendingRefTestsSelected = input.required<boolean>();
  readonly hasInProgressOrCompletedRefTestsSelected = input.required<boolean>();
  readonly hasExpiredRefTestsSelected = input.required<boolean>();
  readonly hasPendingApprovalRefTestsSelected = input.required<boolean>();
  readonly sendingInvitations = input.required<boolean>();
  readonly sendingResults = input.required<boolean>();
  readonly deletingRefTests = input.required<boolean>();
  readonly generatingReport = input.required<boolean>();
  readonly resettingRefTests = input.required<boolean>();
  readonly revivingRefTests = input.required<boolean>();
  readonly approvingRefTests = input.required<boolean>();
  readonly rejectingRefTests = input.required<boolean>();

  protected readonly sendInvitations = output<void>();
  protected readonly sendResults = output<void>();
  protected readonly deleteSelected = output<void>();
  protected readonly generateReport = output<void>();
  protected readonly resetSelected = output<void>();
  protected readonly reviveSelected = output<void>();
  protected readonly approveSelected = output<void>();
  protected readonly rejectSelected = output<void>();

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

  protected onApproveSelected(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.approveSelected.emit();
  }

  protected onRejectSelected(event: Event): void {
    event.stopPropagation();
    this.menuOpen.set(false);
    this.rejectSelected.emit();
  }
}
