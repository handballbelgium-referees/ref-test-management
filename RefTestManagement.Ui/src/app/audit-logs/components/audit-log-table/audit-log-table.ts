import { ChangeDetectionStrategy, Component, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
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

  private readonly _expanded = signal<Set<string>>(new Set());

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
}
