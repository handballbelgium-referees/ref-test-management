import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditLogDtoFilterInput } from '../../../graphql/generated';
import { Permissions } from '../auth/models/permissions';
import { PermissionsService } from '../auth/services/permissions';
import { Banner } from '../shared/components/banner/banner';
import { AuditLogData } from './audit-log-data';
import { AuditLogFilter } from './components/audit-log-filter/audit-log-filter';
import { AuditLogTable } from './components/audit-log-table/audit-log-table';
import { AuditLogEntry } from './types';

@Component({
  selector: 'app-audit-logs',
  imports: [TranslatePipe, Banner, AuditLogFilter, AuditLogTable],
  templateUrl: './list-audit-logs.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ListAuditLogs {
  private readonly _data = inject(AuditLogData);
  private readonly _permissions = inject(PermissionsService);

  protected readonly loading = this._data.loading;
  protected readonly entries = computed<AuditLogEntry[]>(
    () => this._data.queryResult()?.edges?.map((e) => e.node) ?? [],
  );
  protected readonly totalCount = computed(() => this._data.queryResult()?.totalCount ?? 0);
  protected readonly hasNextPage = computed(
    () => this._data.queryResult()?.pageInfo.hasNextPage ?? false,
  );
  protected readonly canLink = computed(() =>
    this._permissions.hasPermission(Permissions.RefTests.ViewDetail),
  );
  protected readonly sortField = computed(() => this._data.sortField());
  protected readonly sortDirection = computed(() => this._data.sortDirection());

  protected onFilterChange(filter: AuditLogDtoFilterInput | null): void {
    this._data.applyFilter(filter);
  }

  protected onSortColumn(field: string): void {
    this._data.applySort(field);
  }

  protected loadMore(): void {
    this._data.loadMore();
  }
}
