import {
  Component,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, debounceTime, map, of, Subject, switchMap } from 'rxjs';
import { SearchQuestionsByNumberGQL } from '../../../../../../graphql/generated';
import { TranslationPipe } from '../../../../pipes/translation-pipe';

interface IQuestion {
  number: string;
  phrase: Record<string, string>;
}

interface ISearchResult {
  searchTerm: string;
  questions: Array<IQuestion & { id: string }>;
  isSearching: boolean;
  error: boolean;
}

@Component({
  selector: 'app-question-search-autocomplete',
  imports: [TranslatePipe, TranslationPipe],
  templateUrl: './question-search-autocomplete.html',
  host: {
    class: 'host',
  },
})
export class QuestionSearchAutocomplete {
  private readonly _searchQuestionsByNumberGQL = inject(SearchQuestionsByNumberGQL);

  readonly currentLanguage = input.required<string>();
  readonly selectedQuestions = input.required<IQuestion[]>();

  protected readonly selectQuestion = output<IQuestion>();
  protected readonly removeQuestion = output<string>();
  protected readonly import = output<void>();

  protected readonly searchTerm = signal('');
  protected readonly suggestions = signal<Array<IQuestion & { id: string }>>([]);
  protected readonly searching = signal(false);
  protected readonly searchError = signal(false);
  protected readonly searchSucceeded = signal(false);
  protected readonly showDropdown = signal(false);
  protected readonly highlightedIndex = signal(-1);

  private readonly _searchSubject = new Subject<string>();

  private readonly _searchResult = toSignal(
    this._searchSubject.pipe(
      debounceTime(300),
      switchMap((searchTerm) => {
        const trimmedTerm = searchTerm.trim();
        if (!trimmedTerm) {
          return of({
            searchTerm: trimmedTerm,
            questions: [],
            isSearching: false,
            error: false,
          } as ISearchResult);
        }

        this.searching.set(true);
        this.searchError.set(false);
        this.searchSucceeded.set(false);
        return this._searchQuestionsByNumberGQL
          .watch({ variables: { number: trimmedTerm }, fetchPolicy: 'cache-and-network' })
          .valueChanges.pipe(
            map((result) => {
              const questions = result.data?.searchQuestionsByNumber ?? [];
              return {
                searchTerm: trimmedTerm,
                questions: questions
                  .filter((q) => !!q)
                  .map((q) => ({
                    id: q!.id,
                    number: q!.number,
                    phrase: q!.phrase as Record<string, string>,
                  })),
                isSearching: result.loading,
                error:
                  !!result.error ||
                  (!result.loading && !result.data?.searchQuestionsByNumber),
              } as ISearchResult;
            }),
            catchError(() =>
              of({
                searchTerm: trimmedTerm,
                questions: [],
                isSearching: false,
                error: true,
              } as ISearchResult),
            ),
          );
      }),
    ),
    { initialValue: { searchTerm: '', questions: [], isSearching: false, error: false } },
  );

  constructor() {
    effect(() => {
      const result = this._searchResult();
      const currentTerm = this.searchTerm().trim();
      if (result.searchTerm !== currentTerm) return;

      if (result.error) {
        this.suggestions.set([]);
        this.searching.set(false);
        this.searchError.set(true);
        this.searchSucceeded.set(false);
        this.showDropdown.set(currentTerm.length > 0);
        return;
      }

      this.suggestions.set(result.questions);
      this.searching.set(result.isSearching);
      this.searchError.set(false);
      this.searchSucceeded.set(!result.isSearching);
      this.showDropdown.set(currentTerm.length > 0);

      // Auto-select first suggestion when results arrive
      if (result.questions.length > 0) {
        this.highlightedIndex.set(0);
      }
    });
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this._searchSubject.next(value);
    this.highlightedIndex.set(-1);
    this.suggestions.set([]);
    this.searching.set(value.trim().length > 0);
    this.searchError.set(false);
    this.searchSucceeded.set(false);
    this.showDropdown.set(value.trim().length > 0);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Tab':
        if (this.showDropdown() && this.suggestions().length > 0) {
          const index = this.highlightedIndex();
          const question = this.suggestions()[index >= 0 ? index : 0];
          if (question) this.onSelectQuestion(question);
        }
        break;
      case 'ArrowDown':
        if (this.showDropdown() && this.suggestions().length > 0) {
          event.preventDefault();
          this.highlightedIndex.update((current) =>
            current < this.suggestions().length - 1 ? current + 1 : 0,
          );
        }
        break;
      case 'ArrowUp':
        if (this.showDropdown() && this.suggestions().length > 0) {
          event.preventDefault();
          this.highlightedIndex.update((current) =>
            current > 0 ? current - 1 : this.suggestions().length - 1,
          );
        }
        break;
      case 'Enter':
        event.preventDefault();
        if (this.showDropdown() && this.suggestions().length > 0) {
          const index = this.highlightedIndex();
          if (index >= 0 && index < this.suggestions().length) {
            this.onSelectQuestion(this.suggestions()[index]);
          }
        }
        break;
      case 'Escape':
        event.preventDefault();
        this.showDropdown.set(false);
        this.highlightedIndex.set(-1);
        break;
    }
  }

  protected onSelectQuestion(question: IQuestion): void {
    this.selectQuestion.emit(question);
    this.searchTerm.set('');
    this.suggestions.set([]);
    this.closeDropdown();
  }

  protected onRemoveQuestion(questionNumber: string): void {
    this.removeQuestion.emit(questionNumber);
  }

  protected onRetrySearch(): void {
    this.searching.set(true);
    this.searchError.set(false);
    this.searchSucceeded.set(false);
    this.showDropdown.set(true);
    this._searchSubject.next(this.searchTerm());
  }

  protected closeDropdown(): void {
    setTimeout(() => {
      this.showDropdown.set(false);
      this.highlightedIndex.set(-1);
    }, 200);
  }
}
