import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConfirmPersonalDataExportGQL } from '../../../../../../graphql/generated';
import { PersonalDataExportConfirmation } from './personal-data-export-confirmation';

describe('PersonalDataExportConfirmation', () => {
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
          dataExport: {
            confirmationTitle: 'Confirm your request',
            confirmationDescription:
              'Confirm below to request an emailed PDF copy of your personal data.',
            confirmButton: 'Confirm request',
            confirming: 'Confirming your request...',
            confirmed:
              'Your request is confirmed. We will attempt to email you a PDF copy of your personal data.',
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

  async function renderConfirmation(): Promise<ComponentFixture<PersonalDataExportConfirmation>> {
    const fixture = TestBed.createComponent(PersonalDataExportConfirmation);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function getConfirmButton(
    fixture: ComponentFixture<PersonalDataExportConfirmation>,
  ): HTMLButtonElement {
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[type="button"]',
    );
    if (!button) throw new Error('The explicit confirmation button was not rendered.');
    return button;
  }

  it('removes the key while preserving the language query and waits for explicit confirmation', async () => {
    const key = 'one-time-confirmation-key';
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
    expect(confirmPersonalDataExportMutation).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Privacy notice');

    getConfirmButton(fixture).click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(confirmPersonalDataExportMutation).toHaveBeenCalledWith({
      variables: { input: { key } },
    });
    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmed')?.textContent,
    ).toContain('Your request is confirmed. We will attempt to email you a PDF copy of your personal data.');
    const successCard = fixture.nativeElement.querySelector(
      '#privacy-data-export-confirmed',
    )?.parentElement;
    expect(successCard?.classList.contains('p-4')).toBe(true);
    expect(successCard?.classList.contains('sm:p-8')).toBe(true);
    expect(successCard?.classList.contains('shadow-sm')).toBe(true);
    expect(successCard?.classList.contains('text-center')).toBe(true);
    expect(successCard?.querySelector('.bg-success-100')).not.toBeNull();
  });

  it('announces pending state before showing a successful confirmation', async () => {
    const confirmationResult = new Subject<{
      loading?: boolean;
      data: {
        confirmPersonalDataExport: {
          personalDataExportConfirmationResult: { confirmed: boolean };
        };
      };
    }>();
    confirmPersonalDataExportMutation.mockReturnValue(confirmationResult.asObservable());
    window.history.replaceState(
      window.history.state,
      '',
      `${window.location.pathname}#pending-key`,
    );

    const fixture = await renderConfirmation();
    const confirmButton = getConfirmButton(fixture);
    confirmButton.click();
    expect(confirmPersonalDataExportMutation).toHaveBeenCalledWith({
      variables: { input: { key: 'pending-key' } },
    });

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
    ).toContain('We will attempt to email you a PDF copy of your personal data.');
  });

  it('keeps the confirmation available and hides server errors on failed attempts', async () => {
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
    const fixture = await renderConfirmation();
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

  it('does not render the confirmation card without a key', async () => {
    const fixture = await renderConfirmation();

    expect(
      fixture.nativeElement.querySelector('#privacy-data-export-confirmation-title'),
    ).toBeNull();
    expect(confirmPersonalDataExportMutation).not.toHaveBeenCalled();
  });
});
