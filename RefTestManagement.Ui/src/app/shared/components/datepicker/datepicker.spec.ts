import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Datepicker as DatepickerService } from './services/datepicker';
import { Datepicker } from './datepicker';

interface IDatepickerHarness {
  isOpen(): boolean;
}

describe('Datepicker focus management', () => {
  let fixture: ComponentFixture<Datepicker>;

  beforeEach(async () => {
    vi.spyOn(DatepickerService.prototype, 'detectSmallTouchDevice').mockReturnValue(true);
    TestBed.configureTestingModule({
      imports: [Datepicker],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        common: {
          choose_date: 'Choose a date',
          change_calendar_view: 'Change calendar view',
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });

    fixture = TestBed.createComponent(Datepicker);
    fixture.detectChanges();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
  });

  it('keeps the mobile calendar closed after selecting a date and restoring focus', async () => {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.focus();
    fixture.detectChanges();

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[role="dialog"][aria-modal="true"]')).not.toBeNull();
    });

    const activeDay = fixture.nativeElement.querySelector(
      'button[data-calendar-day][tabindex="0"]',
    ) as HTMLButtonElement;
    activeDay.focus();
    activeDay.click();
    fixture.detectChanges();

    expect(document.activeElement).toBe(input);
    expect((fixture.componentInstance as unknown as IDatepickerHarness).isOpen()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('keeps the mobile calendar closed after Escape restores focus', async () => {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.focus();
    fixture.detectChanges();

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[role="dialog"][aria-modal="true"]')).not.toBeNull();
    });

    const activeDay = fixture.nativeElement.querySelector(
      'button[data-calendar-day][tabindex="0"]',
    ) as HTMLButtonElement;
    activeDay.focus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(document.activeElement).toBe(input);
    expect((fixture.componentInstance as unknown as IDatepickerHarness).isOpen()).toBe(false);
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('moves focus into each calendar view after changing date granularity', async () => {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.focus();
    fixture.detectChanges();

    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('[role="dialog"][aria-modal="true"]')).not.toBeNull();
    });

    const viewButton = fixture.nativeElement.querySelector(
      'app-calendar-header button[aria-label^="Change calendar view"]',
    ) as HTMLButtonElement;
    const expectFocusOnSelectedView = async (selector: string) => {
      await vi.waitFor(() => {
        fixture.detectChanges();
        const selected = fixture.nativeElement.querySelector(selector) as HTMLElement | null;
        expect(selected).not.toBeNull();
        expect(document.activeElement).toBe(selected);
      });
    };

    viewButton.click();
    await expectFocusOnSelectedView('button[aria-pressed="true"]');
    viewButton.click();
    await expectFocusOnSelectedView('button[aria-pressed="true"]');

    (document.activeElement as HTMLButtonElement).click();
    await expectFocusOnSelectedView('button[aria-pressed="true"]');
    (document.activeElement as HTMLButtonElement).click();
    await expectFocusOnSelectedView('button[data-calendar-day][tabindex="0"]');
  });
});
