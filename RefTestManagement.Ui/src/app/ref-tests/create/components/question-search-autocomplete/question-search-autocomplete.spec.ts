import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SearchQuestionsByNumberGQL } from '../../../../../../graphql/generated';
import { QuestionSearchAutocomplete } from './question-search-autocomplete';

interface IQuestionSearchResult {
  data?: {
    searchQuestionsByNumber?: Array<{
      id: string;
      number: string;
      phrase?: Record<string, string> | null;
    } | null> | null;
  };
  loading: boolean;
  error?: unknown;
}

interface IQuestionAutocompleteHarness {
  onSearchInput(event: Event): void;
}

describe('QuestionSearchAutocomplete', () => {
  const results: Subject<IQuestionSearchResult>[] = [];
  const watch = vi.fn(() => {
    const result = new Subject<IQuestionSearchResult>();
    results.push(result);
    return { valueChanges: result.asObservable() };
  });

  beforeEach(() => {
    results.length = 0;
    watch.mockClear();
    TestBed.configureTestingModule({
      imports: [QuestionSearchAutocomplete],
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        { provide: SearchQuestionsByNumberGQL, useValue: { watch } },
      ],
    });
    TestBed.inject(TranslateService).setTranslation(
      'en',
      {
        common: { retry: 'Retry' },
        ref_tests: {
          create: {
            form: {
              specific_question_numbers: {
                search_placeholder: 'Search questions',
                search_error: 'Could not search questions.',
                no_results: 'No questions found',
                table_number: 'Number',
                table_question: 'Question',
                remove_question: 'Remove question',
              },
            },
          },
        },
      },
      true,
    );
  });

  afterEach(() => TestBed.resetTestingModule());

  it('exposes the active listbox option and selects it with Tab', async () => {
    const fixture: ComponentFixture<QuestionSearchAutocomplete> =
      TestBed.createComponent(QuestionSearchAutocomplete);
    fixture.componentRef.setInput('currentLanguage', 'en');
    fixture.componentRef.setInput('selectedQuestions', []);
    fixture.detectChanges();

    const selected: string[] = [];
    (
      fixture.componentInstance as unknown as {
        selectQuestion: { subscribe(observer: (question: { number: string }) => void): void };
      }
    ).selectQuestion.subscribe((question) => selected.push(question.number));
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = '99.1';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        searchQuestionsByNumber: [
          { id: 'question-1', number: '99.1', phrase: { en: 'First question' } },
          { id: 'question-2', number: '99.2', phrase: { en: 'Second question' } },
        ],
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const listbox = fixture.nativeElement.querySelector('[role="listbox"]') as HTMLElement;
    expect(input.getAttribute('role')).toBe('combobox');
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-controls')).toBe(listbox.id);
    expect(listbox.querySelectorAll('[role="option"]')).toHaveLength(2);

    input.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }),
    );
    fixture.detectChanges();
    expect(input.getAttribute('aria-activedescendant')).toBe('question-search-option-1');

    const tab = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    input.dispatchEvent(tab);
    fixture.detectChanges();
    expect(tab.defaultPrevented).toBe(false);
    expect(selected).toEqual(['99.2']);
    expect(input.getAttribute('aria-expanded')).toBe('false');
  });

  it('shows lookup errors separately from a successful no-match and retries the search', async () => {
    const fixture: ComponentFixture<QuestionSearchAutocomplete> =
      TestBed.createComponent(QuestionSearchAutocomplete);
    fixture.componentRef.setInput('currentLanguage', 'en');
    fixture.componentRef.setInput('selectedQuestions', []);
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as IQuestionAutocompleteHarness;
    component.onSearchInput({ target: { value: '99.1' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());

    results[0].next({ loading: false, error: new Error('lookup failed') });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const failedText = fixture.nativeElement.textContent as string;
    expect(failedText).toContain('Could not search questions.');
    expect(failedText).not.toContain('No questions found');

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(watch).toHaveBeenCalledTimes(2));
    results[1].next({ loading: false, data: { searchQuestionsByNumber: [] } });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No questions found');
    expect(fixture.nativeElement.textContent).not.toContain('Could not search questions.');
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const status = fixture.nativeElement.querySelector(
      '#question-search-empty-results',
    ) as HTMLElement;
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-controls')).toBe(status.id);
    expect(status.getAttribute('role')).toBe('status');
    expect(status.getAttribute('aria-live')).toBe('polite');
  });
});
