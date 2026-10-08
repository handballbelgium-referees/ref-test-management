import { Location } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  GetResultsEmailDelayMinutesGQL,
  GetScoreConfigurationGQL,
} from '../../../../graphql/generated';
import { RefTestFacade } from './state/ref-test.facade';
import { RefTestStore } from './state/ref-test.store';
import { TakeRefTest } from './take-ref-test';

describe('TakeRefTest loading status', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [TakeRefTest],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: Location,
          useValue: { getState: () => ({}) },
        },
        {
          provide: GetScoreConfigurationGQL,
          useValue: {
            watch: () => ({
              valueChanges: of({ data: { scoreConfiguration: { passingPercentage: 70 } } }),
            }),
          },
        },
        {
          provide: GetResultsEmailDelayMinutesGQL,
          useValue: {
            watch: () => ({ valueChanges: of({ data: { resultsEmailDelayMinutes: 10 } }) }),
          },
        },
      ],
    }).overrideComponent(TakeRefTest, {
      set: {
        providers: [
          RefTestStore,
          { provide: RefTestFacade, useValue: { acquireSessionAndStart: vi.fn() } },
        ],
      },
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', { ref_test: { loading: 'Loading RefTest...' } }, true);
    await new Promise<void>((resolve) => translate.use('en').subscribe(() => resolve()));
  });

  afterEach(() => TestBed.resetTestingModule());

  it('exposes the existing loading message as a status and hides the decorative spinner', () => {
    const fixture = TestBed.createComponent(TakeRefTest);
    fixture.componentInstance.store.loading.set(true);
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[role="status"]') as HTMLElement | null;
    const spinner = fixture.nativeElement.querySelector('.animate-spin');

    expect(status?.getAttribute('aria-atomic')).toBe('true');
    expect(status?.textContent?.trim()).toBe('Loading RefTest...');
    expect(spinner?.getAttribute('aria-hidden')).toBe('true');
  });
});
