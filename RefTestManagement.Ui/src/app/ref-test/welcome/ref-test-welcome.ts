import { Location } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  catchError,
  distinctUntilChanged,
  EMPTY,
  finalize,
  map,
  of,
  switchMap,
  take,
  tap,
} from 'rxjs';
import {
  AcceptPrivacyNoticeGQL,
  CreateRefTestSessionGQL,
  GetPrivacyNoticeGQL,
  GetRefTestByTokenGQL,
  WithdrawConsentGQL,
} from '../../../../graphql/generated';
import { toSnakeCase } from '../../shared/utils/string-utils';
import { RefTestError } from '../components/ref-test-error/ref-test-error';
import { WithdrawConsentDialog } from '../components/withdraw-consent-dialog/withdraw-consent-dialog';
import {
  REF_TEST_SESSION_TOKEN_STATE_KEY,
  resolveRefTestToken,
} from '../ref-test-token-state';
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
  private readonly _location = inject(Location);
  private readonly _getRefTestByTokenGQL = inject(GetRefTestByTokenGQL);
  private readonly _getPrivacyNoticeGQL = inject(GetPrivacyNoticeGQL);
  private readonly _acceptPrivacyNoticeGQL = inject(AcceptPrivacyNoticeGQL);
  private readonly _createRefTestSessionGQL = inject(CreateRefTestSessionGQL);
  private readonly _withdrawConsentGQL = inject(WithdrawConsentGQL);
  private readonly _destroyRef = inject(DestroyRef);

  readonly privacyAccepted = signal(false);
  readonly acceptingPrivacyNotice = signal(false);
  readonly privacyNoticeError = signal(false);
  readonly sessionCreationError = signal(false);

  readonly showWithdrawDialog = signal(false);
  readonly withdrawing = signal(false);
  readonly withdrawError = signal(false);
  readonly withdrawalQueued = signal(false);
  private _sessionExchangeCredential: string | null = null;
  private _activeCredential: string | null = null;

  private readonly _token$ = this._route.paramMap.pipe(
    map((params) =>
      resolveRefTestToken(
        params.get('token'),
        this._router.currentNavigation()?.extras.state,
        this._location.getState(),
      ) ?? '',
    ),
    distinctUntilChanged(),
    tap((token) => {
      if (this._activeCredential === token) return;

      this._activeCredential = token;
      this.privacyAccepted.set(false);
      this.privacyNoticeError.set(false);
      this.sessionCreationError.set(false);
      this._sessionExchangeCredential = null;
    }),
  );

  readonly refTestResult = toSignal(
    this._token$.pipe(
      switchMap((token) =>
        this._getRefTestByTokenGQL
          .watch({
            variables: { token },
            fetchPolicy: 'cache-and-network',
          })
          .valueChanges.pipe(
            tap((result) => {
              if (
                result.data?.refTestByToken?.__typename === 'ParticipantRefTest' &&
                (result.data.refTestByToken.status === 'COMPLETED' ||
                  (result.data.refTestByToken.currentQuestionIndex !== null &&
                    result.data.refTestByToken.currentQuestionIndex !== undefined))
              ) {
                this.resumeWithSessionCredential(token);
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
    if (data.__typename === 'ParticipantRefTest') {
      return data;
    }
    return null;
  });

  readonly error = computed(() => {
    const result = this.refTestResult();
    if (!result?.data?.refTestByToken) return null;

    const data = result.data.refTestByToken;
    if (data.__typename && data.__typename !== 'ParticipantRefTest') {
      return toSnakeCase(data.__typename);
    }
    return null;
  });

  readonly loading = computed(() => this.refTestResult()?.loading ?? true);

  readonly canStart = computed(() => {
    const refTest = this.refTest();
    return refTest !== null && !this.loading();
  });

  private readonly _token = toSignal(this._token$, { initialValue: '' });

  startRefTest(): void {
    const token = this._token();
    if (!token || !this.canStart() || !this.privacyAccepted()) return;

    this.acceptingPrivacyNotice.set(true);
    this.privacyNoticeError.set(false);
    this.sessionCreationError.set(false);
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
        switchMap((result) => {
          if (!result.data?.acceptPrivacyNotice.participantRefTest) {
            this.privacyNoticeError.set(true);
            return EMPTY;
          }

          return this.createSessionCredential(token).pipe(
            tap((sessionToken) => {
              if (sessionToken) this.navigateToTake(sessionToken);
              else this.sessionCreationError.set(true);
            }),
          );
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

  private createSessionCredential(token: string) {
    return this._createRefTestSessionGQL
      .mutate({
        variables: { input: { token } },
        useMutationLoading: false,
      })
      .pipe(
        map((result) => {
          const payload = result.data?.createRefTestSession;
          if (!payload?.participantSessionDto || (payload.errors?.length ?? 0) > 0) return null;
          return payload.participantSessionDto.sessionToken;
        }),
        catchError(() => {
          this.sessionCreationError.set(true);
          return of(null);
        }),
      );
  }

  private resumeWithSessionCredential(token: string): void {
    if (this._sessionExchangeCredential === token) return;

    this._sessionExchangeCredential = token;
    this.acceptingPrivacyNotice.set(true);
    this.sessionCreationError.set(false);
    this.createSessionCredential(token)
      .pipe(
        tap((sessionToken) => {
          if (sessionToken) this.navigateToTake(sessionToken);
          else this.sessionCreationError.set(true);
        }),
        finalize(() => this.acceptingPrivacyNotice.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  private navigateToTake(sessionToken: string): void {
    void this._router.navigate(['/ref-test/take'], {
      replaceUrl: true,
      state: { [REF_TEST_SESSION_TOKEN_STATE_KEY]: sessionToken },
    });
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
          const payload = result.data?.withdrawConsent;
          if (!payload?.boolean || (payload.errors && payload.errors.length > 0)) {
            this.withdrawError.set(true);
            return;
          }
          this.showWithdrawDialog.set(false);
          this.withdrawalQueued.set(true);
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
