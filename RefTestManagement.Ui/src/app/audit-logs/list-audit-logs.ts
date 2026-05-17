import { DatePipe, NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditLogDtoFilterInput } from '../../../graphql/generated';
import { Permissions } from '../auth/models/permissions';
import { PermissionsService } from '../auth/services/permissions';
import { Banner } from '../shared/components/banner/banner';
import { AuditLogData } from './audit-log-data';

@Component({
  selector: 'app-audit-logs',
  imports: [TranslatePipe, Banner, RouterLink, DatePipe, NgClass],
  templateUrl: './list-audit-logs.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ListAuditLogs {
  private readonly _data = inject(AuditLogData);
  private readonly _permissions = inject(PermissionsService);

  protected readonly Permissions = Permissions;
  protected readonly loading = this._data.loading;
  protected readonly queryResult = this._data.queryResult;
  protected readonly canViewRefTestDetail = computed(() =>
    this._permissions.hasPermission(Permissions.RefTests.ViewDetail),
  );

  protected readonly filterEntityType = signal('');
  protected readonly filterAction = signal('');
  protected readonly filterActor = signal('');
  protected readonly expandedChanges = signal<Set<string>>(new Set());

  protected readonly entries = computed(() => this.queryResult()?.edges?.map((e) => e.node) ?? []);
  protected readonly totalCount = computed(() => this.queryResult()?.totalCount ?? 0);
  protected readonly hasNextPage = computed(
    () => this.queryResult()?.pageInfo.hasNextPage ?? false,
  );

  protected applyFilters(): void {
    const conditions: AuditLogDtoFilterInput[] = [];

    const entityType = this.filterEntityType().trim();
    if (entityType) conditions.push({ entityType: { contains: entityType } });

    const action = this.filterAction().trim();
    if (action) conditions.push({ action: { eq: action } });

    const actor = this.filterActor().trim();
    if (actor) conditions.push({ actorName: { contains: actor } });

    this._data.applyFilter(conditions.length > 0 ? { and: conditions } : null);
  }

  protected clearFilters(): void {
    this.filterEntityType.set('');
    this.filterAction.set('');
    this.filterActor.set('');
    this._data.applyFilter(null);
  }

  protected loadMore(): void {
    this._data.loadMore();
  }

  protected toggleChanges(id: string): void {
    this.expandedChanges.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected isExpanded(id: string): boolean {
    return this.expandedChanges().has(id);
  }

  protected parseChanges(changesJson: string | null | undefined): ParsedChange[] {
    if (!changesJson) return [];
    try {
      const obj = JSON.parse(changesJson) as Record<string, unknown>;
      return Object.entries(obj)
        .map(([prop, value]) => ({ property: prop, value: value as ChangesValue }))
        .filter(({ value }) => !this.isEmptyChange(value));
    } catch {
      return [];
    }
  }

  protected formatListValue(items: string[]): string {
    if (items.length <= 5) return items.join(', ');
    return `${items.slice(0, 3).join(', ')} … (${items.length} items)`;
  }

  private isEmptyChange(v: ChangesValue): boolean {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const vv = v as any;
    if ('added' in vv) return vv.added.length === 0 && vv.removed.length === 0;
    if ('values' in vv) return vv.values.length === 0;
    const isEmpty = (x: unknown) => x === null || x === undefined || x === '';
    const newEmpty = !('newValue' in vv) || isEmpty(vv.newValue);
    const oldEmpty = !('oldValue' in vv) || isEmpty(vv.oldValue);
    return newEmpty && oldEmpty;
  }

  protected formatActionLabel(action: string): string {
    return action;
  }

  protected actionClass(action: string): string {
    switch (action) {
      case 'Created':
        return 'text-green-700 bg-green-100';
      case 'Deleted':
        return 'text-red-700 bg-red-100';
      default:
        return 'text-blue-700 bg-blue-100';
    }
  }
}

type DiffChange = { added: string[]; removed: string[] };
type ListChange = { values: string[] };
type ScalarChange = { oldValue: unknown; newValue: unknown };
type NewValueChange = { newValue: unknown };
type OldValueChange = { oldValue: unknown };
type ChangesValue = DiffChange | ListChange | ScalarChange | NewValueChange | OldValueChange;

interface ParsedChange {
  property: string;
  value: ChangesValue;
}

export function isDiff(v: ChangesValue): v is DiffChange {
  return 'added' in v && 'removed' in v;
}

export function isList(v: ChangesValue): v is ListChange {
  return 'values' in v;
}

export function isScalar(v: ChangesValue): v is ScalarChange {
  return 'oldValue' in v && 'newValue' in v;
}
