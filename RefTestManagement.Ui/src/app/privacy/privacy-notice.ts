import { computed, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { GetPrivacyNoticeGQL } from '../../../graphql/generated';
import { LocalizedDate } from '../shared/pipes/localized-date';

@Component({
  selector: 'app-privacy-notice',
  imports: [TranslatePipe],
  providers: [LocalizedDate],
  templateUrl: './privacy-notice.html',
  host: {
    class: 'block',
  },
})
export class PrivacyNotice {
  private readonly _getPrivacyNoticeGQL = inject(GetPrivacyNoticeGQL);
  private readonly _localizedDate = inject(LocalizedDate);

  private readonly _privacyNoticeResult = toSignal(this._getPrivacyNoticeGQL.watch().valueChanges, {
    initialValue: null,
  });

  readonly privacyNotice = computed(() => this._privacyNoticeResult()?.data?.privacyNotice ?? null);

  readonly effectiveDate = computed(() => {
    const effectiveDate = this.privacyNotice()?.noticeEffectiveDate;
    return this._localizedDate.transform(effectiveDate, 'longDate') ?? effectiveDate ?? '';
  });
}
