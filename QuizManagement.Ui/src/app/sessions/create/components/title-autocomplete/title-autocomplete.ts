import { ChangeDetectionStrategy, Component, effect, inject, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { QueryRef } from 'apollo-angular';
import { catchError, debounceTime, map, Observable, of, Subject, switchMap } from 'rxjs';
import {
  GetQuizTitlesGQL,
  GetQuizTitlesQuery,
  GetQuizTitlesQueryVariables,
  SortEnumType,
} from '../../../../../../graphql/generated';
import { LocalizedDatePipe } from '../../../../shared/pipes/localized-date.pipe';

interface Title {
  id: string;
  value: string;
}

interface SearchResult {
  titles: Title[];
  isSearching: boolean;
  hasNextPage: boolean;
  endCursor?: string;
}

@Component({
  selector: 'app-title-autocomplete',
  imports: [TranslatePipe, LocalizedDatePipe],
  templateUrl: './title-autocomplete.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TitleAutocomplete {
  private readonly _getQuizTitlesGQL = inject(GetQuizTitlesGQL);

  readonly selectTitle = output<{ id?: string; name: string }>();

  protected readonly currentDate = new Date();

  protected readonly searchTerm = signal('');
  protected readonly suggestions = signal<Title[]>([]);
  protected readonly searching = signal(false);
  protected readonly showDropdown = signal(false);
  protected readonly highlightedIndex = signal(-1);
  protected readonly selectedTitle = signal<{ id?: string; name: string } | null>(null);
  protected readonly hasNextPage = signal(false);
  protected readonly loadingMore = signal(false);
  private _endCursor = signal<string | undefined>(undefined);
  private _queryRef: QueryRef<GetQuizTitlesQuery, GetQuizTitlesQueryVariables> | null = null;

  private readonly _searchSubject = new Subject<string>();

  private readonly _searchResult = toSignal(
    this._searchSubject.pipe(
      debounceTime(300),
      switchMap((searchTerm: string): Observable<SearchResult> => {
        const trimmedTerm = searchTerm.trim();
        const where = trimmedTerm.length > 0 ? { value: { contains: trimmedTerm } } : undefined;

        this._queryRef = this._getQuizTitlesGQL.watch({
          variables: {
            first: 10,
            where,
            order: { value: SortEnumType.Asc },
          },
          fetchPolicy: 'cache-first',
        });

        return this._queryRef.valueChanges.pipe(
          map((result): SearchResult => {
            const edges = result.data?.quizTitles?.edges ?? [];
            const pageInfo = result.data?.quizTitles?.pageInfo;
            return {
              titles: edges
                .filter((edge) => !!edge?.node)
                .map((edge) => ({
                  id: edge!.node!.id!,
                  value: edge!.node!.value!,
                })),
              isSearching: result.loading,
              hasNextPage: pageInfo?.hasNextPage ?? false,
              endCursor: pageInfo?.endCursor ?? undefined,
            };
          }),
          catchError(() => of<SearchResult>({ titles: [], isSearching: false, hasNextPage: false }))
        );
      })
    ),
    { initialValue: { titles: [], isSearching: false, hasNextPage: false } }
  );

  constructor() {
    effect(() => {
      const result = this._searchResult();
      if (!result) return;

      if (this.loadingMore()) {
        this.suggestions.update((current) => [...current, ...result.titles]);
        this.loadingMore.set(false);
      } else {
        this.suggestions.set(result.titles);
      }
      this.searching.set(result.isSearching);
      this.hasNextPage.set(result.hasNextPage);
      this._endCursor.set(result.endCursor);
      this.showDropdown.set(result.titles.length > 0);

      // Auto-select first suggestion when results arrive
      if (result.titles.length > 0 && !this.loadingMore()) {
        this.highlightedIndex.set(0);
      }
    });
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this._searchSubject.next(value);
    this.highlightedIndex.set(-1);
  }

  protected onFocus(): void {
    // Show dropdown immediately if we have suggestions
    if (this.suggestions().length > 0) {
      this.showDropdown.set(true);
    }

    // If we have a query ref, refetch immediately without debounce
    if (this._queryRef) {
      this._queryRef.refetch();
    } else {
      // First time - trigger search (will have debounce delay)
      this._searchSubject.next(this.searchTerm());
    }
  }

  protected onKeyDown(event: KeyboardEvent): void {
    const suggestions = this.suggestions();

    if (event.key === 'Enter' && !this.showDropdown()) {
      event.preventDefault();
      this.onManualEntry();
      return;
    }

    if (!this.showDropdown() || suggestions.length === 0) {
      return;
    }

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.highlightedIndex.update((current) =>
          current < suggestions.length - 1 ? current + 1 : 0
        );
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.highlightedIndex.update((current) =>
          current > 0 ? current - 1 : suggestions.length - 1
        );
        break;
      case 'Enter':
        event.preventDefault();
        const index = this.highlightedIndex();
        if (index >= 0 && index < suggestions.length) {
          this.onSelectTitle(suggestions[index]);
        } else {
          this.onManualEntry();
        }
        break;
      case 'Escape':
        event.preventDefault();
        this.closeDropdown();
        break;
    }
  }

  protected onSelectTitle(title: Title): void {
    const selected = { id: title.id, name: title.value };
    this.selectedTitle.set(selected);
    this.searchTerm.set(title.value);
    this.selectTitle.emit(selected);
    this.closeDropdown();
  }

  protected onManualEntry(): void {
    const term = this.searchTerm().trim();
    if (term.length === 0) {
      return;
    }

    const selected = { name: term };
    this.selectedTitle.set(selected);
    this.selectTitle.emit(selected);
    this.closeDropdown();
  }

  protected onBlur(): void {
    // If there's a search term but no selection, treat it as manual entry
    if (this.searchTerm().trim() && !this.selectedTitle()) {
      this.onManualEntry();
    }
    this.closeDropdown();
  }

  protected closeDropdown(): void {
    this.showDropdown.set(false);
    this.highlightedIndex.set(-1);
  }

  protected onClear(): void {
    this.searchTerm.set('');
    this.selectedTitle.set(null);
    this.suggestions.set([]);
    this.selectTitle.emit({ name: '' });
  }

  protected onScroll(event: Event): void {
    const element = event.target as HTMLElement;
    const atBottom = element.scrollHeight - element.scrollTop <= element.clientHeight + 50;

    if (
      atBottom &&
      this.hasNextPage() &&
      !this.loadingMore() &&
      !this.searching() &&
      this._queryRef
    ) {
      this.loadingMore.set(true);
      this._queryRef.fetchMore({
        variables: {
          after: this._endCursor(),
        },
      });
    }
  }
}
