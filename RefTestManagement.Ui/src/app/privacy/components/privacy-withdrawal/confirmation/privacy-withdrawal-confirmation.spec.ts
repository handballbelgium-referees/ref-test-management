import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConfirmPrivacyWithdrawalGQL } from '../../../../../../graphql/generated';
import { PrivacyWithdrawalConfirmation } from './privacy-withdrawal-confirmation';

describe('PrivacyWithdrawalConfirmation', () => {
  const confirmPrivacyWithdrawalMutation = vi.fn(() =>
    of({
      data: {
        confirmPrivacyWithdrawal: {
          privacyWithdrawalConfirmationResult: { accepted: true },
        },
      },
    }),
  );

  beforeEach(async () => {
    confirmPrivacyWithdrawalMutation.mockReset();
    confirmPrivacyWithdrawalMutation.mockReturnValue(
      of({
        data: {
          confirmPrivacyWithdrawal: {
            privacyWithdrawalConfirmationResult: { accepted: true },
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: ConfirmPrivacyWithdrawalGQL,
          useValue: { mutate: confirmPrivacyWithdrawalMutation },
        },
      ],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        privacy: {
          consentWithdrawal: {
            title: 'Withdraw consent and request anonymization',
            requestLink: 'Open the withdrawal request form',
            confirmationTitle: 'Confirm withdrawal of consent',
            confirmationDescription:
              'Opening this page does not confirm the request. Select Confirm only if you want to withdraw your consent. If accepted, identifying data will be anonymized once processing succeeds; an anonymized RefTest record may be retained for audit purposes. Anonymization is not immediate.',
            confirmButton: 'Confirm withdrawal',
            confirming: 'Confirming your withdrawal request...',
            confirmed:
              'Your consent withdrawal has been confirmed. Identifying data will be anonymized after processing succeeds. An anonymized RefTest record may be retained for audit purposes. Anonymization is not immediate.',
            confirmationError:
              'We could not confirm this request. The link may be invalid, expired, or already used. Request a new link if needed; no participant information is shown here.',
            confirmationKeyMissing:
              'This link does not contain confirmation information. Request a new link from the privacy notice.',
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    window.history.replaceState(window.history.state, '', window.location.pathname);
  });

  async function renderConfirmation(): Promise<
    ComponentFixture<PrivacyWithdrawalConfirmation>
  > {
    const fixture = TestBed.createComponent(PrivacyWithdrawalConfirmation);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function getConfirmButton(
    fixture: ComponentFixture<PrivacyWithdrawalConfirmation>,
  ): HTMLButtonElement {
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="button"]',
    );
    if (!button) throw new Error('The explicit withdrawal confirmation button was not rendered.');
    return button;
  }

  it('removes the fragment from the address bar and waits for an explicit confirmation action', async () => {
    const key = 'z'.repeat(43);
    window.history.replaceState(
      window.history.state,
      '',
      `${window.location.pathname}?lang=nl#${key}`,
    );
    const historyLength = window.history.length;
    const fixture = await renderConfirmation();

    expect(window.location.hash).toBe('');
    expect(window.location.search).toBe('?lang=nl');
    expect(window.location.href).not.toContain(key);
    expect(window.history.length).toBe(historyLength);
    expect(confirmPrivacyWithdrawalMutation).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain(key);

    getConfirmButton(fixture).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(confirmPrivacyWithdrawalMutation).toHaveBeenCalledWith({
      variables: { input: { key } },
    });
    const success = fixture.nativeElement.querySelector('#privacy-withdrawal-confirmed');
    expect(success?.getAttribute('role')).toBe('status');
    expect(success?.textContent).toContain('Your consent withdrawal has been confirmed.');
    expect(success?.textContent).toContain('will be anonymized after processing succeeds');
    expect(success?.textContent).toContain('retained for audit purposes');
  });

  it('announces the pending state while an explicit confirmation is being processed', async () => {
    const key = 'p'.repeat(43);
    const confirmationResult = new Subject<{
      loading?: boolean;
      data: {
        confirmPrivacyWithdrawal: {
          privacyWithdrawalConfirmationResult: { accepted: boolean };
        };
      };
    }>();
    confirmPrivacyWithdrawalMutation.mockReturnValue(confirmationResult.asObservable());
    window.history.replaceState(window.history.state, '', `${window.location.pathname}#${key}`);

    const fixture = await renderConfirmation();
    const confirmButton = getConfirmButton(fixture);
    confirmButton.click();
    fixture.detectChanges();
    confirmationResult.next({
      loading: true,
      data: {
        confirmPrivacyWithdrawal: {
          privacyWithdrawalConfirmationResult: { accepted: false },
        },
      },
    });
    fixture.detectChanges();

    expect(confirmButton.disabled).toBe(true);
    expect(confirmButton.textContent).toContain('Confirming your withdrawal request...');
    expect(
      fixture.nativeElement
        .querySelector('#privacy-withdrawal-confirmation-pending')
        ?.getAttribute('role'),
    ).toBe('status');

    confirmationResult.next({
      loading: false,
      data: {
        confirmPrivacyWithdrawal: {
          privacyWithdrawalConfirmationResult: { accepted: true },
        },
      },
    });
    confirmationResult.complete();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-confirmed'),
    ).not.toBeNull();
  });

  it('does not expose key validity until the user confirms and hides server details', async () => {
    const key = 'not-a-valid-key';
    confirmPrivacyWithdrawalMutation
      .mockReturnValueOnce(
        of({
          data: {
            confirmPrivacyWithdrawal: {
              privacyWithdrawalConfirmationResult: { accepted: false },
            },
          },
        }),
      )
      .mockReturnValueOnce(throwError(() => new Error('sensitive server details')));
    window.history.replaceState(window.history.state, '', `${window.location.pathname}#${key}`);

    const fixture = await renderConfirmation();
    expect(confirmPrivacyWithdrawalMutation).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain(key);

    const confirmButton = getConfirmButton(fixture);
    confirmButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector(
      '#privacy-withdrawal-confirmation-error',
    );
    expect(confirmPrivacyWithdrawalMutation).toHaveBeenCalledWith({
      variables: { input: { key } },
    });
    expect(error?.getAttribute('role')).toBe('alert');
    expect(error?.textContent).toContain('The link may be invalid, expired, or already used.');
    expect(error?.textContent).not.toContain(key);
    expect(error?.textContent).not.toContain('sensitive server details');
    expect(fixture.nativeElement.querySelector('#privacy-withdrawal-confirmed')).toBeNull();

    confirmButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(confirmPrivacyWithdrawalMutation).toHaveBeenCalledTimes(2);
    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-confirmation-error')?.textContent,
    ).not.toContain('sensitive server details');
  });

  it('shows a safe recovery message and performs no operation when the fragment is missing', async () => {
    const fixture = await renderConfirmation();

    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-key-missing')?.textContent,
    ).toContain('This link does not contain confirmation information.');
    expect(fixture.nativeElement.querySelector('button[type="button"]')).toBeNull();
    expect(confirmPrivacyWithdrawalMutation).not.toHaveBeenCalled();
  });
});
