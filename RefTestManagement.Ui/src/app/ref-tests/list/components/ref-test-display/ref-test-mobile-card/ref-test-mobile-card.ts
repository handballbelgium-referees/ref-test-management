import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../graphql/generated';
import { LocalizedDate } from '../../../../../shared/pipes/localized-date';
import { RefTestNode } from '../../../services/types';

@Component({
  selector: 'app-ref-test-mobile-card',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './ref-test-mobile-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block cursor-pointer',
    '(click)': 'onCardClick($event)',
  },
})
export class RefTestMobileCard {
  private readonly router = inject(Router);

  readonly refTest = input.required<RefTestNode>();
  readonly selected = input.required<boolean>();
  readonly visibleColumns = input.required<Set<string>>();
  readonly passingPercentage = input.required<number>();

  protected readonly toggleSelection = output<string>();

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected getStatusClass(status: RefTestStatus): string {
    switch (status) {
      case 'COMPLETED':
        return 'bg-success-100 text-success-800';
      case 'IN_PROGRESS':
        return 'bg-blue-100 text-blue-800';
      case 'EXPIRED':
        return 'bg-red-100 text-red-800';
      case 'PENDING':
      default:
        return 'bg-yellow-100 text-yellow-800';
    }
  }

  protected onToggleSelection(): void {
    this.toggleSelection.emit(this.refTest().id);
  }

  protected onCardClick(event: MouseEvent): void {
    // Don't navigate if clicking on checkbox
    const target = event.target as HTMLElement;
    if (
      target.tagName === 'INPUT' ||
      target.closest('input[type="checkbox"]') ||
      event.ctrlKey ||
      event.metaKey ||
      event.shiftKey
    ) {
      return;
    }

    this.router.navigate(['/ref-tests', this.refTest().id]);
  }
}
