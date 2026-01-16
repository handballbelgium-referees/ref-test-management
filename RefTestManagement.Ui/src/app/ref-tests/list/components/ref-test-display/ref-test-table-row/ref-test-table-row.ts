import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { GetRefTestsQuery, RefTestStatus } from '../../../../../../../graphql/generated';
import { LocalizedDate } from '../../../../../shared/pipes/localized-date';

type RefTestNode = NonNullable<
  NonNullable<NonNullable<GetRefTestsQuery['refTests']>['edges']>[number]
>['node'];

@Component({
  selector: 'tr[app-ref-test-table-row]',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './ref-test-table-row.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'hover:bg-neutral-50 transition-colors cursor-pointer',
    '(click)': 'onRowClick($event)',
  },
})
export class RefTestTableRow {
  private readonly router = inject(Router);

  readonly refTest = input.required<RefTestNode>();
  readonly selected = input.required<boolean>();
  readonly visibleColumns = input.required<Set<string>>();
  readonly passingPercentage = input.required<number>();

  readonly toggleSelection = output<string>();

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected getStatusClass(status: RefTestStatus): string {
    switch (status) {
      case RefTestStatus.Completed:
        return 'bg-green-100 text-green-800';
      case RefTestStatus.InProgress:
        return 'bg-blue-100 text-blue-800';
      case RefTestStatus.Expired:
        return 'bg-red-100 text-red-800';
      case RefTestStatus.Pending:
      default:
        return 'bg-yellow-100 text-yellow-800';
    }
  }

  protected onToggleSelection(): void {
    this.toggleSelection.emit(this.refTest().id);
  }

  protected onRowClick(event: MouseEvent): void {
    // Don't navigate if clicking on checkbox or if it's a modifier click
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
