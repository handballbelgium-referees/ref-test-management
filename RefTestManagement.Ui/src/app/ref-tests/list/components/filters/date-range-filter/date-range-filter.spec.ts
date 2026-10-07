import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Datepicker as DatepickerService } from '../../../../../shared/components/datepicker/services/datepicker';
import { DateRangeFilter } from './date-range-filter';

describe('DateRangeFilter', () => {
  beforeEach(async () => {
    vi.spyOn(DatepickerService.prototype, 'detectSmallTouchDevice').mockReturnValue(false);
    TestBed.configureTestingModule({
      imports: [DateRangeFilter],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        common: { choose_date: 'Choose a date' },
        ref_tests: { list: { filters: { from: 'From', to: 'To' } } },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
  });

  it('gives date-range fields distinct accessible names', async () => {
    const fixture: ComponentFixture<DateRangeFilter> = TestBed.createComponent(DateRangeFilter);
    fixture.componentRef.setInput('label', 'ref_tests.list.filters.from');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const inputs = fixture.nativeElement.querySelectorAll('input');
    expect(inputs[0].getAttribute('aria-label')).toBe('From');
    expect(inputs[1].getAttribute('aria-label')).toBe('To');
  });

  it('uses unique dialog targets and closes the previous calendar on focus change', async () => {
    const fixture: ComponentFixture<DateRangeFilter> = TestBed.createComponent(DateRangeFilter);
    fixture.componentRef.setInput('label', 'ref_tests.list.filters.from');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const inputs = fixture.nativeElement.querySelectorAll('input');
    const fromInput = inputs[0] as HTMLInputElement;
    const toInput = inputs[1] as HTMLInputElement;
    fromInput.focus();
    fixture.detectChanges();

    const fromDialogId = fromInput.getAttribute('aria-controls');
    expect(fromDialogId).not.toBeNull();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(document.getElementById(fromDialogId!)).not.toBeNull();
    });

    toInput.focus();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(fromInput.getAttribute('aria-expanded')).toBe('false');
      expect(toInput.getAttribute('aria-expanded')).toBe('true');
    });

    const toDialogId = toInput.getAttribute('aria-controls');
    expect(toDialogId).not.toBeNull();
    expect(toDialogId).not.toBe(fromDialogId);
    expect(document.getElementById(toDialogId!)).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('[role="dialog"]')).toHaveLength(1);
  });
});
