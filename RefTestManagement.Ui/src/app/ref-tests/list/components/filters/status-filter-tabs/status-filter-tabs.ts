import {
  Component,
  effect,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../../graphql/generated';
import { HasPermission } from '../../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../../auth/models/permissions';

interface IStatusCounts {
  all: number;
  pending: number;
  inProgress: number;
  completed: number;
  expired: number;
  pendingApproval: number;
  rejected: number;
}

@Component({
  selector: 'app-status-filter-tabs',
  imports: [TranslatePipe, HasPermission],
  templateUrl: './status-filter-tabs.html',
  host: {
    class: 'block',
  },
})
export class StatusFilterTabs {
  readonly selectedStatus = input<RefTestStatus | undefined>();
  readonly statusCounts = input.required<IStatusCounts>();
  protected readonly statusChange = output<RefTestStatus | undefined>();

  protected readonly Permissions = Permissions;

  protected readonly filterScroll = viewChild<ElementRef<HTMLDivElement>>('filterScroll');
  protected readonly canScrollLeft = signal(false);
  protected readonly canScrollRight = signal(false);

  constructor() {
    effect(() => {
      const scrollEl = this.filterScroll();
      if (scrollEl) {
        setTimeout(() => this.checkScrollPosition(), 0);
      }
    });
  }

  protected scrollFilters(direction: 'left' | 'right'): void {
    const container = this.filterScroll()?.nativeElement;
    if (!container) return;

    const scrollAmount = container.clientWidth * 0.8;
    container.scrollBy({
      left: direction === 'left' ? -scrollAmount : scrollAmount,
      behavior: 'smooth',
    });

    setTimeout(() => this.checkScrollPosition(), 300);
  }

  protected onFilterScroll(): void {
    this.checkScrollPosition();
  }

  private checkScrollPosition(): void {
    const container = this.filterScroll()?.nativeElement;
    if (!container) return;

    const scrollLeft = container.scrollLeft;
    const maxScroll = container.scrollWidth - container.clientWidth;

    this.canScrollLeft.set(scrollLeft > 1);
    this.canScrollRight.set(scrollLeft < maxScroll - 1);
  }

  protected onStatusChange(status?: RefTestStatus, buttonElement?: EventTarget | null): void {
    this.statusChange.emit(status);

    if (buttonElement && buttonElement instanceof HTMLElement) {
      const container = this.filterScroll()?.nativeElement;
      if (!container) return;

      const buttonLeft = buttonElement.offsetLeft;
      const buttonWidth = buttonElement.offsetWidth;
      const containerWidth = container.clientWidth;

      // Always center the clicked button
      const targetScroll = buttonLeft - containerWidth / 2 + buttonWidth / 2;

      container.scrollTo({
        left: targetScroll,
        behavior: 'smooth',
      });

      setTimeout(() => this.checkScrollPosition(), 300);
    }
  }
}
