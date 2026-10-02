import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RequestPrivacyWithdrawalGQL } from '../../../graphql/generated';
import { PrivacyWithdrawalRequest } from './privacy-withdrawal-request';

describe('PrivacyWithdrawalRequest', () => {
  const requestPrivacyWithdrawalMutation = vi.fn(() =>
    of({
      data: {
        requestPrivacyWithdrawal: {
          privacyWithdrawalRequestAcknowledgement: { acknowledged: true },
        },
      },
    }),
  );

  beforeEach(async () => {
    requestPrivacyWithdrawalMutation.mockReset();
    requestPrivacyWithdrawalMutation.mockReturnValue(
      of({
        data: {
          requestPrivacyWithdrawal: {
            privacyWithdrawalRequestAcknowledgement: { acknowledged: true },
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: RequestPrivacyWithdrawalGQL,
          useValue: { mutate: requestPrivacyWithdrawalMutation },
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
            description:
              'Enter the email address associated with your participation. No account or invitation token is needed.',
            emailLabel: 'Email address',
            emailHelp:
              'After you confirm, identifying data will be anonymized once processing succeeds; an anonymized RefTest record may be retained for audit purposes.',
            emailRequired: 'Enter your email address.',
            emailInvalid: 'Enter a valid email address.',
            requestButton: 'Send withdrawal request',
            requesting: 'Sending your request...',
            requestAcknowledged:
              'If the address is associated with a participant record, a confirmation email will be sent. This page shows the same acknowledgement whether or not a record matches. After you confirm, identifying data will be anonymized once processing succeeds. An anonymized RefTest record may be retained for audit purposes.',
            requestError: 'We could not process your request. Please try again.',
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

  async function renderRequest(): Promise<ComponentFixture<PrivacyWithdrawalRequest>> {
    const fixture = TestBed.createComponent(PrivacyWithdrawalRequest);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function getEmailInput(
    fixture: ComponentFixture<PrivacyWithdrawalRequest>,
  ): HTMLInputElement {
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '#privacy-withdrawal-email',
    );
    if (!input) throw new Error('The consent withdrawal email input was not rendered.');
    return input;
  }

  function submitRequest(fixture: ComponentFixture<PrivacyWithdrawalRequest>): void {
    const form = (fixture.nativeElement as HTMLElement).querySelector<HTMLFormElement>('form');
    if (!form) throw new Error('The consent withdrawal request form was not rendered.');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
  }

  function setEmail(fixture: ComponentFixture<PrivacyWithdrawalRequest>, email: string): void {
    const input = getEmailInput(fixture);
    input.value = email;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }

  it('renders a separate public request form without participant or confirmation details', async () => {
    const fixture = await renderRequest();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Withdraw consent and request anonymization');
    expect(text).toContain('No account or invitation token is needed.');
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('#privacy-withdrawal-confirmation-title')).toBeNull();
  });

  it('shows localized validation and does not send invalid addresses', async () => {
    const fixture = await renderRequest();

    submitRequest(fixture);
    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-email-error')?.textContent,
    ).toContain('Enter your email address.');
    expect(getEmailInput(fixture).getAttribute('aria-invalid')).toBe('true');
    expect(requestPrivacyWithdrawalMutation).not.toHaveBeenCalled();

    setEmail(fixture, 'not-an-email');
    submitRequest(fixture);
    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-email-error')?.textContent,
    ).toContain('Enter a valid email address.');
    expect(requestPrivacyWithdrawalMutation).not.toHaveBeenCalled();
  });

  it('sends only the email and always renders the same neutral acknowledgement', async () => {
    const fixture = await renderRequest();
    setEmail(fixture, 'participant@example.com');
    await fixture.componentInstance.requestWithdrawal();
    fixture.detectChanges();

    const acknowledgement = fixture.nativeElement.querySelector(
      '#privacy-withdrawal-request-acknowledged',
    );
    expect(requestPrivacyWithdrawalMutation).toHaveBeenCalledWith({
      variables: { input: { input: { email: 'participant@example.com' } } },
    });
    expect(acknowledgement?.getAttribute('role')).toBe('status');
    expect(acknowledgement?.textContent).toContain(
      'This page shows the same acknowledgement whether or not a record matches.',
    );
    expect(acknowledgement?.textContent).toContain('will be anonymized once processing succeeds');
    expect(acknowledgement?.textContent).toContain('retained for audit purposes');
    expect(acknowledgement?.textContent).not.toContain('participant@example.com');
    expect(fixture.nativeElement.querySelector('form')).toBeNull();

    await fixture.componentInstance.requestWithdrawal();
    expect(requestPrivacyWithdrawalMutation).toHaveBeenCalledTimes(1);
  });

  it('announces a localized pending state until the acknowledgement arrives', async () => {
    const requestResult = new Subject<{
      loading?: boolean;
      data: {
        requestPrivacyWithdrawal: {
          privacyWithdrawalRequestAcknowledgement: { acknowledged: boolean };
        };
      };
    }>();
    requestPrivacyWithdrawalMutation.mockReturnValue(requestResult.asObservable());

    const fixture = await renderRequest();
    setEmail(fixture, 'participant@example.com');
    const requestSubmission = fixture.componentInstance.requestWithdrawal();
    fixture.detectChanges();
    requestResult.next({
      loading: true,
      data: {
        requestPrivacyWithdrawal: {
          privacyWithdrawalRequestAcknowledgement: { acknowledged: false },
        },
      },
    });
    fixture.detectChanges();

    const submitButton = fixture.nativeElement.querySelector(
      'form button[type="submit"]',
    ) as HTMLButtonElement;
    expect(submitButton.disabled).toBe(true);
    expect(submitButton.textContent).toContain('Sending your request...');
    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-request-pending')?.getAttribute('role'),
    ).toBe('status');

    requestResult.next({
      loading: false,
      data: {
        requestPrivacyWithdrawal: {
          privacyWithdrawalRequestAcknowledgement: { acknowledged: true },
        },
      },
    });
    requestResult.complete();
    await requestSubmission;
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelector('#privacy-withdrawal-request-acknowledged'),
    ).not.toBeNull();
  });

  it('hides server details on request errors', async () => {
    requestPrivacyWithdrawalMutation.mockReturnValue(
      throwError(() => new Error('sensitive server details')),
    );
    const fixture = await renderRequest();
    setEmail(fixture, 'participant@example.com');
    await fixture.componentInstance.requestWithdrawal();
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('#privacy-withdrawal-request-error');
    expect(error?.getAttribute('role')).toBe('alert');
    expect(error?.textContent).toContain('We could not process your request. Please try again.');
    expect(error?.textContent).not.toContain('sensitive server details');
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });
});
