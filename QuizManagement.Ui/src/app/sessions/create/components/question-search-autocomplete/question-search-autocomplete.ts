import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { catchError, debounceTime, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import { SearchQuestionsByNumberGQL } from '../../../../../../graphql/generated';
import { TranslationPipe } from '../../../../pipes/translation-pipe';

interface Question {
  number: string;
  phrase: Record<string, string>;
}

@Component({
  selector: 'app-question-search-autocomplete',
  imports: [TranslatePipe, TranslationPipe],
  templateUrl: './question-search-autocomplete.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuestionSearchAutocomplete {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _searchQuestionsByNumberGQL = inject(SearchQuestionsByNumberGQL);

  readonly currentLanguage = input.required<string>();
  readonly selectedQuestions = input.required<Question[]>();

  readonly selectQuestion = output<Question>();
  readonly removeQuestion = output<string>();
  readonly bulkImport = output<void>();

  protected readonly searchTerm = signal('');
  protected readonly searching = signal(false);
  protected readonly suggestions = signal<Array<Question & { id: string }>>([]);
  protected readonly showDropdown = signal(false);
  protected readonly highlightedIndex = signal(-1);

  private readonly searchSubject = new Subject<string>();

  constructor() {
    this.searchSubject
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((searchTerm) => {
          if (!searchTerm || searchTerm.trim().length === 0) {
            return of([]);
          }

          this.searching.set(true);
          return this._searchQuestionsByNumberGQL
            .fetch({
              variables: { number: searchTerm },
            })
            .pipe(
              map((result) => {
                const questions = result.data?.searchQuestionsByNumber ?? [];
                return questions
                  .filter((q) => !!q)
                  .map((q) => ({
                    id: q!.id,
                    number: q!.number,
                    phrase: q!.phrase as Record<string, string>,
                  }));
              }),
              catchError(() => of([]))
            );
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((questions) => {
        this.suggestions.set(questions);
        this.searching.set(false);
        this.showDropdown.set(questions.length > 0 || this.searchTerm().trim().length > 0);
      });
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this.searchSubject.next(value);
    this.highlightedIndex.set(-1);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (!this.showDropdown() || this.suggestions().length === 0) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.highlightedIndex.update((current) =>
          current < this.suggestions().length - 1 ? current + 1 : 0
        );
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.highlightedIndex.update((current) =>
          current > 0 ? current - 1 : this.suggestions().length - 1
        );
        break;
      case 'Enter':
        event.preventDefault();
        const index = this.highlightedIndex();
        if (index >= 0 && index < this.suggestions().length) {
          this.onSelectQuestion(this.suggestions()[index]);
        }
        break;
      case 'Escape':
        event.preventDefault();
        this.closeDropdown();
        break;
    }
  }

  protected onSelectQuestion(question: Question): void {
    this.selectQuestion.emit(question);
    this.searchTerm.set('');
    this.suggestions.set([]);
    this.closeDropdown();
  }

  protected onRemoveQuestion(questionNumber: string): void {
    this.removeQuestion.emit(questionNumber);
  }

  protected closeDropdown(): void {
    setTimeout(() => {
      this.showDropdown.set(false);
      this.highlightedIndex.set(-1);
    }, 200);
  }
}
