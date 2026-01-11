import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-pull-to-refresh',
  imports: [TranslatePipe],
  templateUrl: './pull-to-refresh.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block relative',
  },
})
export class PullToRefresh {
  readonly onRefresh = output<void>();
  readonly isRefreshing = input<boolean>(false);

  protected readonly isPulling = signal(false);
  protected readonly pullDistance = signal(0);
  protected readonly startY = signal(0);
  protected readonly maxPullDistance = 120;
  protected readonly triggerDistance = 80;

  protected onTouchStart(event: TouchEvent): void {
    if (window.scrollY === 0) {
      this.startY.set(event.touches[0].clientY);
      this.isPulling.set(true);
    }
  }

  protected onTouchMove(event: TouchEvent): void {
    if (!this.isPulling() || this.isRefreshing()) {
      return;
    }

    const currentY = event.touches[0].clientY;
    const deltaY = currentY - this.startY();

    // Only pull down when at the top
    if (window.scrollY === 0 && deltaY > 0) {
      event.preventDefault();
      const distance = Math.min(deltaY * 0.5, this.maxPullDistance);
      this.pullDistance.set(distance);
    } else {
      this.resetPull();
    }
  }

  protected onTouchEnd(): void {
    if (!this.isPulling() || this.isRefreshing()) {
      return;
    }

    if (this.pullDistance() >= this.triggerDistance) {
      this.onRefresh.emit();
    }

    this.resetPull();
  }

  private resetPull(): void {
    this.isPulling.set(false);
    this.pullDistance.set(0);
    this.startY.set(0);
  }

  protected get pullProgress(): number {
    return Math.min(this.pullDistance() / this.triggerDistance, 1);
  }

  protected get isTriggered(): boolean {
    return this.pullDistance() >= this.triggerDistance;
  }
}
