import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, map, switchMap, take, tap } from 'rxjs';
import {
  AcceptPrivacyNoticeGQL,
  GetPrivacyNoticeGQL,
  GetRefTestByTokenGQL,
  WithdrawConsentGQL,
} from '../../../../graphql/generated';
import { toSnakeCase } from '../../shared/utils/string-utils';
import { RefTestError } from '../components/ref-test-error/ref-test-error';
import { WithdrawConsentDialog } from '../components/withdraw-consent-dialog/withdraw-consent-dialog';
import { RefTestDetails } from './components/ref-test-details/ref-test-details';
import { RefTestHero } from './components/ref-test-hero/ref-test-hero';
import { RefTestInstructions } from './components/ref-test-instructions/ref-test-instructions';

@Component({
  selector: 'app-ref-test-welcome',
  imports: [
    TranslatePipe,
    RefTestHero,
    RefTestError,
    RefTestDetails,
    RefTestInstructions,
    WithdrawConsentDialog,
  ],
  templateUrl: './ref-test-welcome.html',
  host: {
    class: 'block',
  },
})
export class RefTestWelcome {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _getRefTestByTokenGQL = inject(GetRefTestByTokenGQL);
  private readonly _getPrivacyNoticeGQL = inject(GetPrivacyNoticeGQL);
  private readonly _acceptPrivacyNoticeGQL = inject(AcceptPrivacyNoticeGQL);
  private readonly _withdrawConsentGQL = inject(WithdrawConsentGQL);
  private readonly _destroyRef = inject(DestroyRef);

  readonly privacyAccepted = signal(false);
  readonly acceptingPrivacyNotice = signal(false);
  readonly privacyNoticeError = signal(false);

  readonly showWithdrawDialog = signal(false);
  readonly withdrawing = signal(false);
  readonly withdrawError = signal(false);
  readonly withdrawn = signal(false);

  readonly refTestResult = toSignal(
    this._route.paramMap.pipe(
      map((params) => params.get('token') ?? ''),
      switchMap((token) =>
        this._getRefTestByTokenGQL
          .watch({
            variables: { token },
            fetchPolicy: 'cache-and-network',
          })
          .valueChanges.pipe(
            tap((result) => {
              if (
                result.data?.refTestByToken?.__typename === 'RefTest' &&
                result.data.refTestByToken.currentQuestionIndex !== null &&
                result.data.refTestByToken.currentQuestionIndex !== undefined
              ) {
                this._router.navigate(['/ref-test', token, 'take']);
              }
            }),
          ),
      ),
    ),
    { initialValue: null },
  );

  readonly refTest = computed(() => {
    const result = this.refTestResult();
    if (!result?.data?.refTestByToken) return null;

    const data = result.data.refTestByToken;
    if (data.__typename === 'RefTest') {
      return data;
    }
    return null;
  });

  readonly error = computed(() => {
    const result = this.refTestResult();
    if (!result?.data?.refTestByToken) return null;

    const data = result.data.refTestByToken;
    if (data.__typename && data.__typename !== 'RefTest') {
      return toSnakeCase(data.__typename);
    }
    return null;
  });

  readonly loading = computed(() => this.refTestResult()?.loading ?? true);

  readonly canStart = computed(() => {
    const refTest = this.refTest();
    return refTest !== null && !this.loading();
  });

  private readonly _token = toSignal(
    this._route.paramMap.pipe(map((params) => params.get('token') ?? '')),
  );

  startRefTest(): void {
    const token = this._token();
    if (!token || !this.canStart() || !this.privacyAccepted()) return;

    this.acceptingPrivacyNotice.set(true);
    this.privacyNoticeError.set(false);
    this._getPrivacyNoticeGQL
      .fetch()
      .pipe(
        take(1),
        switchMap((noticeResult) => {
          const noticeVersion = noticeResult.data?.privacyNotice?.noticeVersion;
          if (!noticeVersion) {
            this.privacyNoticeError.set(true);
            return EMPTY;
          }

          return this._acceptPrivacyNoticeGQL.mutate({
            variables: { input: { token, noticeVersion } },
            useMutationLoading: false,
          });
        }),
        tap((result) => {
          if (result.data?.acceptPrivacyNotice.refTest) {
            this._router.navigate(['/ref-test', token, 'take']);
            return;
          }
          this.privacyNoticeError.set(true);
        }),
        catchError(() => {
          this.privacyNoticeError.set(true);
          return EMPTY;
        }),
        finalize(() => this.acceptingPrivacyNotice.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  withdrawConsent(): void {
    const token = this._token();
    if (!token) return;

    this.withdrawError.set(false);
    this.withdrawing.set(true);
    this._withdrawConsentGQL
      .mutate({ variables: { input: { token } } })
      .pipe(
        take(1),
        tap((result) => {
          const errors = result.data?.withdrawConsent.errors;
          if (errors && errors.length > 0) {
            this.withdrawError.set(true);
            return;
          }
          this.showWithdrawDialog.set(false);
          this.withdrawn.set(true);
        }),
        catchError(() => {
          this.withdrawError.set(true);
          return EMPTY;
        }),
        finalize(() => this.withdrawing.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
