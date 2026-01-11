import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { map, switchMap, tap } from 'rxjs';
import { GetRefTestByTokenGQL } from '../../../../graphql/generated';
import { RefTestError } from '../components/ref-test-error/ref-test-error';
import { RefTestDetails } from './components/ref-test-details/ref-test-details';
import { RefTestHero } from './components/ref-test-hero/ref-test-hero';
import { RefTestInstructions } from './components/ref-test-instructions/ref-test-instructions';

@Component({
  selector: 'app-ref-test-welcome',
  imports: [TranslatePipe, RefTestHero, RefTestError, RefTestDetails, RefTestInstructions],
  templateUrl: './ref-test-welcome.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class RefTestWelcome {
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _getRefTestByTokenGQL = inject(GetRefTestByTokenGQL);

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
            })
          )
      )
    ),
    { initialValue: null }
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
    if (data.__typename !== 'RefTest') {
      const snakeCaseValue = data.__typename
        ?.replace(/([A-Z])/g, '_$1')
        .toLowerCase()
        .replace(/^_/, '');
      return snakeCaseValue;
    }
    return null;
  });

  readonly loading = computed(() => this.refTestResult()?.loading ?? true);

  readonly canStart = computed(() => {
    const refTest = this.refTest();
    return refTest !== null && !this.loading();
  });

  private readonly _token = toSignal(
    this._route.paramMap.pipe(map((params) => params.get('token') ?? ''))
  );

  startRefTest(): void {
    const token = this._token();
    if (!token || !this.canStart()) return;

    this._router.navigate(['/ref-test', token, 'take']);
  }
}
