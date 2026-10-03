import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RequestPersonalDataExportGQL } from '../../../../../../graphql/generated';
import { PersonalDataExportRequest } from './personal-data-export-request';

describe('PersonalDataExportRequest', () => {
  const requestPersonalDataExportMutation = vi.fn(() =>
    of({
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: true },
        },
      },
    }),
  );

  beforeEach(async () => {
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

    TestBed.configureTestingModule({
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: RequestPersonalDataExportGQL,
          useValue: { mutate: requestPersonalDataExportMutation },
        },
      ],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        privacy: {
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

  async function renderRequest(): Promise<ComponentFixture<PersonalDataExportRequest>> {
    const fixture = TestBed.createComponent(PersonalDataExportRequest);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function getEmailInput(fixture: ComponentFixture<PersonalDataExportRequest>): HTMLInputElement {
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '#privacy-data-export-email',
    );
    if (!input) throw new Error('The privacy data request email input was not rendered.');
    return input;
  }

  function submitRequest(fixture: ComponentFixture<PersonalDataExportRequest>): void {
    const form = (fixture.nativeElement as HTMLElement).querySelector<HTMLFormElement>('form');
    if (!form) throw new Error('The privacy data request form was not rendered.');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
  }

  function setEmail(fixture: ComponentFixture<PersonalDataExportRequest>, email: string): void {
    const input = getEmailInput(fixture);
    input.value = email;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }

  it('renders only the request card without privacy notice or confirmation content', async () => {
    const fixture = await renderRequest();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Request a copy of your personal data');
    expect(text).not.toContain('Privacy notice');
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmation-title'),
    ).toBeNull();
  });

  it('shows localized validation and does not request data for invalid email input', async () => {
    const fixture = await renderRequest();

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

    const fixture = await renderRequest();
    setEmail(fixture, 'participant@example.com');
    await fixture.componentInstance.requestPersonalDataExport();
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
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    const successCard = fixture.nativeElement
      .querySelector('#privacy-data-export-request-acknowledged')
      ?.parentElement;
    expect(successCard?.classList.contains('p-4')).toBe(true);
    expect(successCard?.classList.contains('sm:p-8')).toBe(true);
    expect(successCard?.classList.contains('shadow-sm')).toBe(true);
    expect(successCard?.classList.contains('text-center')).toBe(true);
    expect(successCard?.querySelector('.bg-success-100')).not.toBeNull();
    await fixture.componentInstance.requestPersonalDataExport();
    fixture.detectChanges();
    expect(requestPersonalDataExportMutation).toHaveBeenCalledTimes(1);
  });

  it('shows a localized request error without exposing the server error', async () => {
    requestPersonalDataExportMutation.mockReturnValue(
      throwError(() => new Error('sensitive server details')),
    );
    const fixture = await renderRequest();
    setEmail(fixture, 'participant@example.com');
    await fixture.componentInstance.requestPersonalDataExport();
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('#privacy-data-export-request-error');
    expect(error?.getAttribute('role')).toBe('alert');
    expect(error?.textContent).toContain('We could not process your request. Please try again.');
    expect(error?.textContent).not.toContain('sensitive server details');
    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
  });

  it('announces the pending state before the request completes', async () => {
    const requestResult = new Subject<{
      loading?: boolean;
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: boolean };
        };
      };
    }>();
    requestPersonalDataExportMutation.mockReturnValue(requestResult.asObservable());

    const fixture = await renderRequest();
    const requestButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'form button[type="submit"]',
    );
    if (!requestButton) throw new Error('The data request button was not rendered.');

    setEmail(fixture, 'participant@example.com');
    const requestSubmission = fixture.componentInstance.requestPersonalDataExport();
    fixture.detectChanges();
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
    expect(requestButton.textContent).toContain('Sending your request...');
    expect(requestButton.querySelector('.animate-spin')?.getAttribute('aria-hidden')).toBe('true');

    requestResult.next({
      loading: false,
      data: {
        requestPersonalDataExport: {
          personalDataExportRequestAcknowledgement: { acknowledged: true },
        },
      },
    });
    requestResult.complete();
    await requestSubmission;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-request-acknowledged'),
    ).not.toBeNull();
  });
});
