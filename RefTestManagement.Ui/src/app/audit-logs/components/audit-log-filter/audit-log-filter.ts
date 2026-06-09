import { Component, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditLogDtoFilterInput } from '../../../../../graphql/generated';

@Component({
  selector: 'app-audit-log-filter',
  imports: [TranslatePipe],
  templateUrl: './audit-log-filter.html',
  host: { class: 'block' },
})
export class AuditLogFilter {
  readonly filterChange = output<AuditLogDtoFilterInput | null>();

  protected readonly streamId = signal('');
  protected readonly type = signal('');
  protected readonly actor = signal('');

  protected onStreamIdChange(value: string): void {
    this.streamId.set(value);
    this._emit();
  }

  protected onTypeChange(value: string): void {
    this.type.set(value);
    this._emit();
  }

  protected onActorChange(value: string): void {
    this.actor.set(value);
    this._emit();
  }

  private _emit(): void {
    const conditions: AuditLogDtoFilterInput[] = [];

    const streamId = this.streamId().trim();
    if (streamId) conditions.push({ streamId: { contains: streamId } });

    const type = this.type().trim();
    if (type) conditions.push({ type: { eq: type } });

    const actor = this.actor().trim();
    if (actor) conditions.push({ actorName: { contains: actor } });

    this.filterChange.emit(conditions.length > 0 ? { and: conditions } : null);
  }
}
