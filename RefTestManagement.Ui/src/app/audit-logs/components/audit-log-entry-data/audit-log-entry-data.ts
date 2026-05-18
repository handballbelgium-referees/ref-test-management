import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { map, startWith } from 'rxjs';
import { LocalizedDate } from '../../../shared/pipes/localized-date';
import { ParsedChange } from '../../types';

@Component({
  selector: 'app-audit-log-entry-data',
  providers: [LocalizedDate],
  templateUrl: './audit-log-entry-data.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'contents' },
})
export class AuditLogEntryData {
  private readonly _localizedDate = inject(LocalizedDate);
  private readonly _translate = inject(TranslateService);

  private readonly _lang = toSignal(
    this._translate.onLangChange.pipe(
      map((e) => e.lang),
      startWith(this._translate.getCurrentLang()),
    ),
  );

  readonly data = input.required<string>();

  protected readonly parsedChanges = computed<ParsedChange[]>(() => {
    this._lang(); // track language so labels re-render on language switch
    try {
      const obj = JSON.parse(this.data()) as Record<string, unknown>;
      return Object.entries(obj)
        .map(([property, value]) => ({ property, value }))
        .filter(({ value }) => value !== null && value !== undefined);
    } catch {
      return [];
    }
  });

  protected formatLabel(key: string): string {
    const translationKey = `audit-logs.field.${key}`;
    const translated = this._translate.instant(translationKey);
    if (translated !== translationKey) return translated;
    // Fallback: split camelCase into Title Case
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, (c) => c.toUpperCase())
      .trim();
  }

  protected isOldNew(value: unknown): value is { old: unknown; new: unknown } {
    return (
      typeof value === 'object' && value !== null && 'old' in value && 'new' in (value as object)
    );
  }

  protected formatValue(value: unknown): string {
    if (value === null || value === undefined) return '';
    if (typeof value === 'boolean') {
      return this._translate.instant(value ? 'common.yes' : 'common.no');
    }
    const str = String(value);
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(str)) {
      return this._localizedDate.transform(str, 'dd/MM/yyyy HH:mm:ss') ?? str;
    }
    return str;
  }
}
