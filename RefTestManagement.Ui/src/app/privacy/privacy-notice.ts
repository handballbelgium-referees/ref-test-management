import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, map, of, startWith, Subject, switchMap } from 'rxjs';
import { GetPrivacyNoticeGQL } from '../../../graphql/generated';
import { LocalizedDate } from '../shared/pipes/localized-date';

@Component({
  selector: 'app-privacy-notice',
  imports: [RouterLink, TranslatePipe],
  providers: [LocalizedDate],
  templateUrl: './privacy-notice.html',
  host: {
    class: 'block',
  },
})
export class PrivacyNotice {
  private readonly _getPrivacyNoticeGQL = inject(GetPrivacyNoticeGQL);
  private readonly _localizedDate = inject(LocalizedDate);
  private readonly _retry = new Subject<void>();

  private readonly _privacyNoticeResult = toSignal(
    this._retry.pipe(
      startWith(undefined),
      switchMap(() =>
        this._getPrivacyNoticeGQL
          .watch()
          .valueChanges.pipe(
            map((result) => ({
              notice: result.data?.privacyNotice ?? null,
              loading: result.loading,
              error: result.error ?? null,
            })),
            catchError((error: unknown) => of({ notice: null, loading: false, error })),
          ),
      ),
    ),
    { initialValue: { notice: null, loading: true, error: null } },
  );

  readonly privacyNotice = computed(() => this._privacyNoticeResult().notice);
  readonly loading = computed(() => this._privacyNoticeResult().loading);
  readonly error = computed(() => this._privacyNoticeResult().error);

  protected retry(): void {
    this._retry.next();
  }

  readonly effectiveDate = computed(() => {
    const effectiveDate = this.privacyNotice()?.noticeEffectiveDate;
    return this._localizedDate.transform(effectiveDate, 'longDate') ?? effectiveDate ?? '';
  });
}
