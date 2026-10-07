import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Datepicker as DatepickerService } from '../../../datepicker/services/datepicker';
import { TimePicker } from './time-picker';

interface ITimePickerHarness {
  valueChange: {
    subscribe(observer: (value: string) => void): void;
  };
}

describe('TimePicker keyboard interaction', () => {
  let fixture: ComponentFixture<TimePicker>;

  beforeEach(() => {
    vi.spyOn(DatepickerService.prototype, 'detectSmallTouchDevice').mockReturnValue(false);
    TestBed.configureTestingModule({
      imports: [TimePicker],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        common: {
          clear: 'Clear',
          time_picker: {
            label: 'Time picker',
            back_to_hours: 'Back to hours',
            close: 'Close time picker',
            hour: '{{hour}} hours',
            minute: '{{minute}} minutes',
            subtract_minute: 'Subtract 1 minute',
            add_minute: 'Add 1 minute',
            one_minute_short: '1 min',
          },
        },
      },
      true,
    );
    translate.use('en').subscribe();

    fixture = TestBed.createComponent(TimePicker);
    fixture.componentRef.setInput('value', '09:25');
    fixture.componentRef.setInput('label', 'Scheduled time');
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
  });

  it('labels keyboard-selectable times and returns focus when the picker closes', async () => {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const emitted: string[] = [];
    (fixture.componentInstance as unknown as ITimePickerHarness).valueChange.subscribe((value) =>
      emitted.push(value),
    );

    input.focus();
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    expect(input.getAttribute('aria-label')).toBe('Scheduled time');
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-controls')).toBe('time-picker-dialog');
    expect(dialog.getAttribute('aria-modal')).toBeNull();

    const tenHours = Array.from(
      dialog.querySelectorAll<HTMLButtonElement>('[data-time-option]'),
    ).find((button) => button.getAttribute('aria-label') === '10 hours');
    expect(tenHours?.tabIndex).toBe(0);
    tenHours?.click();
    fixture.componentRef.setInput('value', '10:25');
    fixture.detectChanges();
    expect(emitted).toEqual(['10:25']);

    const host = fixture.nativeElement as HTMLElement;
    const thirtyMinutes = Array.from(
      host.querySelectorAll<HTMLButtonElement>('[data-time-option]'),
    ).find((button) => button.getAttribute('aria-label') === '30 minutes');
    thirtyMinutes?.click();
    fixture.detectChanges();
    expect(emitted).toEqual(['10:25', '10:30']);
    expect(input.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(input);
  });

  it('does not reopen the mobile picker when selection restores focus', async () => {
    vi.spyOn(DatepickerService.prototype, 'detectSmallTouchDevice').mockReturnValue(true);
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.focus();
    fixture.detectChanges();

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[role="dialog"][aria-modal="true"]')).not.toBeNull();
    });

    const host = fixture.nativeElement as HTMLElement;
    const dialog = host.querySelector('[role="dialog"][aria-modal="true"]');
    expect(dialog?.closest('[aria-hidden="true"]')).toBeNull();
    const tenHours = Array.from(
      host.querySelectorAll<HTMLButtonElement>('[data-time-option]'),
    ).find((button) => button.getAttribute('aria-label') === '10 hours');
    expect(tenHours).toBeDefined();
    tenHours!.click();
    fixture.componentRef.setInput('value', '10:25');
    fixture.detectChanges();

    const backToHours = host.querySelector(
      'button[aria-label="Back to hours"]',
    ) as HTMLButtonElement;
    backToHours.focus();
    backToHours.click();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(document.activeElement?.getAttribute('aria-label')).toBe('10 hours');
    });

    const selectedHour = Array.from(
      host.querySelectorAll<HTMLButtonElement>('[data-time-option]'),
    ).find((button) => button.getAttribute('aria-label') === '10 hours');
    selectedHour?.click();
    fixture.detectChanges();

    const thirtyMinutes = Array.from(
      host.querySelectorAll<HTMLButtonElement>('[data-time-option]'),
    ).find((button) => button.getAttribute('aria-label') === '30 minutes');
    expect(thirtyMinutes).toBeDefined();
    thirtyMinutes!.focus();
    thirtyMinutes!.click();
    fixture.detectChanges();

    expect(input.getAttribute('aria-expanded')).toBe('false');
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(document.activeElement).toBe(input);
  });
});
