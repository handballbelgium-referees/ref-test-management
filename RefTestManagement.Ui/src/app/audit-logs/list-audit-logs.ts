import { Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditLogDtoFilterInput, SortEnumType } from '../../../graphql/generated';
import { Permissions } from '../auth/models/permissions';
import { PermissionsService } from '../auth/services/permissions';
import { Banner } from '../shared/components/banner/banner';
import { AuditLogData } from './audit-log-data';
import { AuditLogFilter } from './components/audit-log-filter/audit-log-filter';
import { AuditLogSortPanel } from './components/audit-log-sort-panel/audit-log-sort-panel';
import { AuditLogTable } from './components/audit-log-table/audit-log-table';
import { AuditLogEntry } from './types';

@Component({
  selector: 'app-audit-logs',
  imports: [TranslatePipe, Banner, AuditLogFilter, AuditLogSortPanel, AuditLogTable],
  templateUrl: './list-audit-logs.html',
  host: { class: 'block' },
})
export class ListAuditLogs {
  private readonly _data = inject(AuditLogData);
  private readonly _permissions = inject(PermissionsService);

  protected readonly loading = this._data.loading;
  protected readonly queryError = this._data.queryError;
  protected readonly loadingMore = this._data.loadingMore;
  protected readonly loadMoreError = this._data.loadMoreError;
  protected readonly entries = computed<AuditLogEntry[]>(
    () =>
      this._data.queryResult().data?.edges
        ?.map((edge) => edge?.node)
        .filter((entry): entry is AuditLogEntry => !!entry) ?? [],
  );
  protected readonly totalCount = computed(
    () => this._data.queryResult().data?.totalCount ?? 0,
  );
  protected readonly hasNextPage = computed(
    () => this._data.queryResult().data?.pageInfo?.hasNextPage ?? false,
  );
  protected readonly isInitialLoading = computed(
    () => this.loading() && this.entries().length === 0,
  );
  protected readonly canLink = computed(() =>
    this._permissions.hasPermission(Permissions.RefTests.ViewDetail),
  );
  protected readonly sortField = computed(() => this._data.sortField());
  protected readonly sortDirection = computed(() => this._data.sortDirection());
  protected readonly isFiltered = computed(() => this._data.filter() !== null);

  protected onFilterChange(filter: AuditLogDtoFilterInput | null): void {
    this._data.applyFilter(filter);
  }

  protected onSortingChange(event: { field: string; direction: SortEnumType }): void {
    this._data.applyFullSort(event.field, event.direction);
  }

  protected onSortColumn(field: string): void {
    this._data.applySort(field);
  }

  protected loadMore(): void {
    this._data.loadMore();
  }

  protected retry(): void {
    this._data.retry();
  }
}
