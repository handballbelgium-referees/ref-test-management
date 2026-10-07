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
  });
});
