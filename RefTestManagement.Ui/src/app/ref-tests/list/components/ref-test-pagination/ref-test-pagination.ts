import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Pagination component displaying result count, load more button, and max capacity message.
 * Manages infinite scroll loading for the ref test list.
 */
@Component({
  selector: 'app-ref-test-pagination',
  imports: [TranslatePipe],
  templateUrl: './ref-test-pagination.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestPagination {
  readonly displayedCount = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly canLoadMore = input.required<boolean>();
  readonly isAtMaxCapacity = input.required<boolean>();
  readonly loadingMore = input.required<boolean>();
  readonly maxLoadableItems = input.required<number>();

  readonly loadMore = output<void>();
}
