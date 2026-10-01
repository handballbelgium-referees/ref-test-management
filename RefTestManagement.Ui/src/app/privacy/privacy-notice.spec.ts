import { registerLocaleData } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import {
  ConfirmPersonalDataExportGQL,
  GetPrivacyNoticeGQL,
  RequestPersonalDataExportGQL,
} from '../../../graphql/generated';
import { PrivacyNotice } from './privacy-notice';

describe('PrivacyNotice', () => {
  const notice = {
    controllerName: 'Handball Belgium',
    controllerAddress: 'Arena 1, 1000 Brussels, Belgium',
    contactEmail: 'privacy@example.com',
    noticeVersion: 'v2.3',
    noticeEffectiveDate: '2026-09-22',
    retentionYears: 5,
  };
  const privacyNoticeWatch = vi.fn(() => ({
    valueChanges: of({
      data: {
        privacyNotice: notice,
      },
    }),
  }));
  const privacyNoticeFetch = vi.fn();
  const requestPersonalDataExportMutation = vi.fn(() =>
    of({
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: true },
        },
      },
    }),
  );
  const confirmPersonalDataExportMutation = vi.fn(() =>
    of({
      data: {
        confirmPersonalDataExport: {
          personalDataExportConfirmationResult: { confirmed: true },
        },
      },
    }),
  );

  beforeEach(async () => {
    const frenchLocale = await import('@angular/common/locales/fr');
    registerLocaleData(frenchLocale.default, 'fr-BE');

    privacyNoticeWatch.mockReset();
    privacyNoticeWatch.mockReturnValue({
      valueChanges: of({
        data: {
          privacyNotice: notice,
        },
      }),
    });
    privacyNoticeFetch.mockReset();
    requestPersonalDataExportMutation.mockReset();
    requestPersonalDataExportMutation.mockReturnValue(
      of({
        data: {
          requestPersonalDataExport: {
            personalDataExportRequestAcknowledgement: { acknowledged: true },
          },
        },
      }),
    );
    confirmPersonalDataExportMutation.mockReset();
    confirmPersonalDataExportMutation.mockReturnValue(
      of({
        data: {
          confirmPersonalDataExport: {
            personalDataExportConfirmationResult: { confirmed: true },
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: GetPrivacyNoticeGQL,
          useValue: {
            watch: privacyNoticeWatch,
            fetch: privacyNoticeFetch,
          },
        },
        {
          provide: RequestPersonalDataExportGQL,
          useValue: { mutate: requestPersonalDataExportMutation },
        },
        {
          provide: ConfirmPersonalDataExportGQL,
          useValue: { mutate: confirmPersonalDataExportMutation },
        },
      ],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        privacy: {
          title: 'Privacy notice',
          updated: 'Effective from {{effectiveDate}} (version {{noticeVersion}}).',
          controller: {
            title: 'Data controller and contact',
            body: '{{controllerName}}, {{controllerAddress}}. Contact {{contactEmail}}.',
          },
          data: { title: 'Data', body: 'Data body' },
          purposes: { title: 'Purposes', body: 'Purposes body' },
          basis: { title: 'Basis', body: 'Basis body' },
          recipients: { title: 'Recipients', body: 'Recipients body' },
          retention: { title: 'Retention', body: 'Retained for {{retentionYears}} years.' },
          rights: { title: 'Rights', body: 'Contact {{contactEmail}}.' },
          dataExport: {
            title: 'Request a copy of your personal data',
            description:
              'Enter the email address associated with your participation. If it matches a participant record, we will send a confirmation link to that address.',
            emailLabel: 'Email address',
            emailHelp: 'We will use this address only to verify and deliver your request.',
            emailRequired: 'Enter your email address.',
            emailInvalid: 'Enter a valid email address.',
            requestButton: 'Send request',
            requesting: 'Sending your request...',
            requestAcknowledged:
              'If the address matches a participant record, you will receive an email with a link to confirm your request.',
            requestError: 'We could not process your request. Please try again.',
            confirmationTitle: 'Confirm your request',
            confirmationDescription:
              'Confirm below to have a PDF copy of your personal data sent by email.',
            confirmButton: 'Confirm request',
            confirming: 'Confirming your request...',
            confirmed:
              'Your request is confirmed. A PDF copy of your personal data will be emailed to you.',
            confirmationError:
              'We could not confirm this request. The link may be invalid or expired. Please request a new link and try again.',
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

  async function renderPrivacyNotice(): Promise<ComponentFixture<PrivacyNotice>> {
    const fixture = TestBed.createComponent(PrivacyNotice);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function getEmailInput(fixture: ComponentFixture<PrivacyNotice>): HTMLInputElement {
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '#privacy-data-export-email',
    );
    if (!input) throw new Error('The privacy data request email input was not rendered.');
    return input;
  }

  function submitRequest(fixture: ComponentFixture<PrivacyNotice>): void {
    const form = (fixture.nativeElement as HTMLElement).querySelector<HTMLFormElement>('form');
    if (!form) throw new Error('The privacy data request form was not rendered.');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
  }

  function setEmail(fixture: ComponentFixture<PrivacyNotice>, email: string): void {
    const input = getEmailInput(fixture);
    input.value = email;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }

  function getConfirmButton(fixture: ComponentFixture<PrivacyNotice>): HTMLButtonElement {
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="button"]',
    );
    if (!button) throw new Error('The explicit confirmation button was not rendered.');
    return button;
  }

  it('renders the backend privacy notice metadata instead of hard-coded copy', async () => {
    const fixture = await renderPrivacyNotice();

    const text = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(text).toMatch(/Effective from .*2026.*\(version v2\.3\)\./);
    expect(text).toContain(
      'Handball Belgium, Arena 1, 1000 Brussels, Belgium. Contact privacy@example.com.',
    );
    expect(text).toContain('Retained for 5 years.');
    expect(text).toContain('Contact privacy@example.com.');
  });

  it('shows localized validation and does not request data for invalid email input', async () => {
    const fixture = await renderPrivacyNotice();

    submitRequest(fixture);
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-email-error')?.textContent,
    ).toContain('Enter your email address.');
    expect(getEmailInput(fixture).getAttribute('aria-invalid')).toBe('true');
    expect(requestPersonalDataExportMutation).not.toHaveBeenCalled();

    setEmail(fixture, 'not-an-email');
    submitRequest(fixture);
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-email-error')?.textContent,
    ).toContain('Enter a valid email address.');
    expect(requestPersonalDataExportMutation).not.toHaveBeenCalled();
  });

  it('sends only the email and always shows the same generic request acknowledgement', async () => {
    const translate = TestBed.inject(TranslateService);
    await new Promise<void>((resolve) => translate.use('fr').subscribe(() => resolve()));

    const fixture = await renderPrivacyNotice();
    setEmail(fixture, 'participant@example.com');
    submitRequest(fixture);
    await fixture.whenStable();
    fixture.detectChanges();

    const acknowledgement = fixture.nativeElement.querySelector(
      '#privacy-data-export-request-acknowledged',
    )?.textContent;
    expect(requestPersonalDataExportMutation).toHaveBeenCalledWith({
      variables: {
        input: {
          input: {
            email: 'participant@example.com',
          },
        },
      },
    });
    expect(acknowledgement).toContain(
      'If the address matches a participant record, you will receive an email with a link to confirm your request.',
    );
    expect(acknowledgement).not.toContain('participant@example.com');

    setEmail(fixture, 'another@example.com');
    submitRequest(fixture);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-request-acknowledged')?.textContent,
    ).toBe(acknowledgement);
  });

  it('shows a localized request error without exposing the server error', async () => {
    requestPersonalDataExportMutation.mockReturnValue(
      throwError(() => new Error('sensitive server details')),
    );
    const fixture = await renderPrivacyNotice();
    setEmail(fixture, 'participant@example.com');
    submitRequest(fixture);
    await fixture.whenStable();
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('#privacy-data-export-request-error');
    expect(error?.getAttribute('role')).toBe('alert');
    expect(error?.textContent).toContain('We could not process your request. Please try again.');
    expect(error?.textContent).not.toContain('sensitive server details');
  });

  it('removes the confirmation key from the URL and sends it only after explicit confirmation', async () => {
    const key = 'one-time-confirmation-key';
    window.history.replaceState(
      window.history.state,
      '',
      `${window.location.pathname}?lang=nl#${key}`,
    );
    const historyLength = window.history.length;
    const fixture = await renderPrivacyNotice();

    expect(window.location.hash).toBe('');
    expect(window.location.search).toBe('?lang=nl');
    expect(window.location.href).not.toContain(key);
    expect(window.history.length).toBe(historyLength);
    expect(privacyNoticeWatch).toHaveBeenCalledTimes(1);
    expect(privacyNoticeWatch.mock.calls[0]).toEqual([]);
    expect(privacyNoticeFetch).not.toHaveBeenCalled();
    expect(confirmPersonalDataExportMutation).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain(key);

    const confirmButton = getConfirmButton(fixture);
    confirmButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(confirmPersonalDataExportMutation).toHaveBeenCalledWith({
      variables: { input: { key } },
    });
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmed')?.textContent,
    ).toContain(
      'Your request is confirmed. A PDF copy of your personal data will be emailed to you.',
    );
    expect(privacyNoticeWatch).toHaveBeenCalledTimes(1);
    expect(privacyNoticeFetch).not.toHaveBeenCalled();
  });

  it('keeps the confirmation available and shows localized errors for failed attempts', async () => {
    window.history.replaceState(
      window.history.state,
      '',
      `${window.location.pathname}#expired-key`,
    );
    confirmPersonalDataExportMutation
      .mockReturnValueOnce(
        of({
          data: {
            confirmPersonalDataExport: {
              personalDataExportConfirmationResult: { confirmed: false },
            },
          },
        }),
      )
      .mockReturnValueOnce(throwError(() => new Error('sensitive server details')));
    const fixture = await renderPrivacyNotice();
    const confirmButton = getConfirmButton(fixture);

    confirmButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmation-error')?.textContent,
    ).toContain(
      'We could not confirm this request. The link may be invalid or expired. Please request a new link and try again.',
    );
    expect(fixture.nativeElement.querySelector('#privacy-data-export-confirmed')).toBeNull();

    confirmButton.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(confirmPersonalDataExportMutation).toHaveBeenCalledTimes(2);
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmation-error')?.textContent,
    ).not.toContain('sensitive server details');
  });

  it('announces pending states before request and confirmation complete', async () => {
    const requestResult = new Subject<{
      loading?: boolean;
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: boolean };
        };
      };
    }>();
    const confirmationResult = new Subject<{
      loading?: boolean;
      data: {
        confirmPersonalDataExport: {
          personalDataExportConfirmationResult: { confirmed: boolean };
        };
      };
    }>();
    requestPersonalDataExportMutation.mockReturnValue(requestResult.asObservable());
    confirmPersonalDataExportMutation.mockReturnValue(confirmationResult.asObservable());
    window.history.replaceState(
      window.history.state,
      '',
      `${window.location.pathname}#pending-key`,
    );

    const fixture = await renderPrivacyNotice();
    const requestButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'form button[type="submit"]',
    );
    if (!requestButton) throw new Error('The data request button was not rendered.');

    setEmail(fixture, 'participant@example.com');
    submitRequest(fixture);
    requestResult.next({
      loading: true,
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: false },
        },
      },
    });
    fixture.detectChanges();
    expect(requestButton.disabled).toBe(true);
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-request-pending')?.textContent,
    ).toContain('Sending your request...');

    requestResult.next({
      loading: false,
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: true },
        },
      },
    });
    requestResult.complete();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(requestButton.disabled).toBe(false);
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-request-acknowledged'),
    ).not.toBeNull();

    const confirmButton = getConfirmButton(fixture);
    confirmButton.click();
    confirmationResult.next({
      loading: true,
      data: {
        confirmPersonalDataExport: {
          personalDataExportConfirmationResult: { confirmed: false },
        },
      },
    });
    fixture.detectChanges();
    expect(confirmButton.disabled).toBe(true);
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmation-pending')?.textContent,
    ).toContain('Confirming your request...');

    confirmationResult.next({
      loading: false,
      data: {
        confirmPersonalDataExport: {
          personalDataExportConfirmationResult: { confirmed: true },
        },
      },
    });
    confirmationResult.complete();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmed')?.textContent,
    ).toContain('A PDF copy of your personal data will be emailed to you.');
  });
});
