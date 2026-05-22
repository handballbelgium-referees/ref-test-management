import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { SortEnumType } from '../../../../../graphql/generated';
import { LocalizedDate } from '../../../shared/pipes/localized-date';
import { AuditLogEntry } from '../../types';
import { AuditLogEntryData } from '../audit-log-entry-data/audit-log-entry-data';
import { AuditLogEventBadge } from '../audit-log-event-badge/audit-log-event-badge';

@Component({
  selector: 'app-audit-log-table',
  imports: [TranslatePipe, RouterLink, LocalizedDate, AuditLogEventBadge, AuditLogEntryData],
  templateUrl: './audit-log-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class AuditLogTable {
  readonly entries = input.required<AuditLogEntry[]>();
  readonly canLink = input<boolean>(false);
  readonly sortField = input<string>('seqId');
  readonly sortDirection = input<SortEnumType>('DESC');

  readonly sortColumn = output<string>();

  private readonly _expanded = signal<Set<string>>(new Set());
  private readonly _copiedId = signal<string | null>(null);

  protected toggle(id: string): void {
    this._expanded.update((set) => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected isExpanded(id: string): boolean {
    return this._expanded().has(id);
  }

  protected canLinkEntry(entry: AuditLogEntry): boolean {
    return this.canLink() && entry.nodeId != null;
  }

  protected async copyToClipboard(text: string, id: string): Promise<void> {
    await navigator.clipboard.writeText(text);
    this._copiedId.set(id);
    setTimeout(() => {
      if (this._copiedId() === id) this._copiedId.set(null);
    }, 2000);
  }

  protected isCopied(id: string): boolean {
    return this._copiedId() === id;
  }

  protected canShowDetails(entry: AuditLogEntry): boolean {
    return !!entry.data || this.canLinkEntry(entry) || this.getEntityType(entry) !== null;
  }

  protected getAriaSort(field: string): 'none' | 'ascending' | 'descending' {
    if (this.sortField() !== field) return 'none';
    return this.sortDirection() === 'ASC' ? 'ascending' : 'descending';
  }

  protected getEntityType(entry: AuditLogEntry): string | null {
    if (!entry.headers) return null;
    try {
      const h = JSON.parse(entry.headers) as { entityType?: string };
      return h.entityType ?? null;
    } catch {
      return null;
    }
  }
}
