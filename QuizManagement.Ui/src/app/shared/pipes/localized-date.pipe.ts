import { DatePipe } from '@angular/common';
import { inject, Pipe, PipeTransform } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Pipe({
  name: 'localizedDate',
  pure: false, // Makes pipe impure so it updates when language changes
})
export class LocalizedDatePipe implements PipeTransform {
  private readonly _translate = inject(TranslateService);

  transform(value: Date | string | number | null | undefined, format?: string): string | null {
    if (!value) return null;

    const locale = `${this._translate.getCurrentLang()}-BE` || 'en-BE';
    const datePipe = new DatePipe(locale);

    // Use custom format with slashes if 'short' or 'shortDate' is requested
    let actualFormat = format || 'short';
    if (format === 'short') {
      actualFormat = 'dd/MM/yyyy, HH:mm';
    } else if (format === 'shortDate') {
      actualFormat = 'dd/MM/yyyy';
    }

    return datePipe.transform(value, actualFormat);
  }
}
