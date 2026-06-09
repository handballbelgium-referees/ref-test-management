import { Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { SortEnumType } from '../../../../../graphql/generated';

@Component({
  selector: 'app-audit-log-sort-panel',
  imports: [TranslatePipe],
  templateUrl: './audit-log-sort-panel.html',
  host: { class: 'block' },
  styles: `
    select {
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
      background-position: right 0.5rem center;
      background-repeat: no-repeat;
      background-size: 1.5em 1.5em;
    }
  `,
})
export class AuditLogSortPanel {
  readonly sortField = input.required<string>();
  readonly sortDirection = input.required<SortEnumType>();

  readonly sortingChange = output<{ field: string; direction: SortEnumType }>();

  protected readonly expanded = signal(false);

  protected togglePanel(): void {
    this.expanded.update((v) => !v);
  }

  protected onSortFieldChange(value: string): void {
    this.sortingChange.emit({ field: value, direction: this.sortDirection() });
  }

  protected onSortDirectionChange(value: string): void {
    this.sortingChange.emit({ field: this.sortField(), direction: value as SortEnumType });
  }
}
