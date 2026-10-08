import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { Datepicker } from '../../../../../../services/datepicker';
import { DaysGrid } from './days-grid';

interface IDaysGridHarness {
  dateSelect: {
    subscribe(observer: (date: Date) => void): void;
  };
}

describe('DaysGrid keyboard interaction', () => {
  let fixture: ComponentFixture<DaysGrid>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DaysGrid],
      providers: [Datepicker, provideTranslateService({ fallbackLang: 'en' })],
    });
    const translate = TestBed.inject(TranslateService);
    translate.addLangs(['en']);
    translate.use('en').subscribe();

    fixture = TestBed.createComponent(DaysGrid);
    fixture.componentRef.setInput('currentDate', new Date(2026, 3, 15));
    fixture.componentRef.setInput('selectedDate', new Date(2026, 3, 15));
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('uses a labelled roving grid and selects the next date with the keyboard', () => {
    const grid = fixture.nativeElement.querySelector('[role="grid"]') as HTMLElement;
    const selectedCell = fixture.nativeElement.querySelector(
      '[role="gridcell"][aria-selected="true"]',
    ) as HTMLElement;
    const activeDay = selectedCell.querySelector('button') as HTMLButtonElement;
    const selected: Date[] = [];
    (fixture.componentInstance as unknown as IDaysGridHarness).dateSelect.subscribe((date) =>
      selected.push(date),
    );

    expect(grid.getAttribute('aria-label')).toBe('April 2026');
    expect(activeDay.getAttribute('aria-label')).toBe('Wednesday, April 15, 2026');
    expect(activeDay.tabIndex).toBe(0);
    expect(fixture.nativeElement.querySelectorAll('button[tabindex="0"]')).toHaveLength(1);

    activeDay.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }),
    );
    fixture.detectChanges();

    const nextDay = fixture.nativeElement.querySelector(
      'button[tabindex="0"]',
    ) as HTMLButtonElement;
    expect(nextDay.getAttribute('aria-label')).toBe('Thursday, April 16, 2026');
    nextDay.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }),
    );
    expect(selected).toEqual([new Date(2026, 3, 16)]);
  });
});
