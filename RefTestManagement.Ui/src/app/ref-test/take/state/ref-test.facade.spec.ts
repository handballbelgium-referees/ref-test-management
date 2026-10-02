import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  CompleteRefTestGQL,
  GetRefTestByTokenGQL,
  RefTestSessionLockGQL,
  RefTestTimeExtendedGQL,
  SaveRefTestProgressGQL,
  StartRefTestGQL,
  WithdrawConsentGQL,
} from '../../../../../graphql/generated';
import { RefTestFacade } from './ref-test.facade';
import { RefTestStore } from './ref-test.store';

describe('RefTestFacade consent withdrawal', () => {
  const sessionToken = 'rts1.session-token';
  let facade: RefTestFacade;
  let store: RefTestStore;
  let withdrawalMutation: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    withdrawalMutation = vi.fn().mockReturnValue(
      of({
        data: {
          withdrawConsent: {
            boolean: true,
            errors: [],
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        RefTestStore,
        RefTestFacade,
        { provide: TranslateService, useValue: { currentLang: () => 'en' } },
        { provide: StartRefTestGQL, useValue: {} },
        { provide: CompleteRefTestGQL, useValue: {} },
        { provide: GetRefTestByTokenGQL, useValue: {} },
        { provide: SaveRefTestProgressGQL, useValue: {} },
        { provide: RefTestTimeExtendedGQL, useValue: {} },
        { provide: RefTestSessionLockGQL, useValue: {} },
        { provide: WithdrawConsentGQL, useValue: { mutate: withdrawalMutation } },
      ],
    });

    facade = TestBed.inject(RefTestFacade);
    store = TestBed.inject(RefTestStore);
  });

  it('shows the queued state only after the API accepts the durable request', () => {
    store.token.set(sessionToken);
    store.showWithdrawDialog.set(true);

    facade.withdrawConsent();

    expect(withdrawalMutation).toHaveBeenCalledWith({
      variables: { input: { token: sessionToken } },
    });
    expect(store.withdrawalQueued()).toBe(true);
    expect(store.showWithdrawDialog()).toBe(false);
  });

  it('keeps the request in the confirmation state when the API does not accept it', () => {
    withdrawalMutation.mockReturnValue(
      of({
        data: {
          withdrawConsent: {
            boolean: false,
            errors: [],
          },
        },
      }),
    );
    store.token.set(sessionToken);
    store.showWithdrawDialog.set(true);

    facade.withdrawConsent();

    expect(store.withdrawError()).toBe(true);
    expect(store.withdrawalQueued()).toBe(false);
    expect(store.showWithdrawDialog()).toBe(true);
  });
});
