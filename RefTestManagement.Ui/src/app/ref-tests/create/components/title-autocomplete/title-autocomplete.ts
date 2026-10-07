import {
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { QueryRef } from 'apollo-angular';
import { catchError, debounceTime, map, Observable, of, Subject, switchMap } from 'rxjs';
import {
  GetRefTestTitlesGQL,
  GetRefTestTitlesQuery,
  GetRefTestTitlesQueryVariables,
} from '../../../../../../graphql/generated';
import { LocalizedDate } from '../../../../shared/pipes/localized-date';

interface ITitle {
  id: string;
  value: string;
}

interface ISearchResult {
  searchTerm: string;
  titles: ITitle[];
  isSearching: boolean;
  hasNextPage: boolean;
  endCursor?: string;
  error: boolean;
}

@Component({
  selector: 'app-title-autocomplete',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './title-autocomplete.html',
  host: {
    class: 'host',
  },
})
export class TitleAutocomplete {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _getRefTestTitlesGQL = inject(GetRefTestTitlesGQL);

  readonly initialTitle = input<{ id?: string; name: string } | null>(null);
  readonly selectTitle = output<{ id?: string; name: string }>();

  protected readonly currentDate = new Date();

  protected readonly searchTerm = signal('');
  protected readonly suggestions = signal<ITitle[]>([]);
  private readonly _pendingBlur = signal(false);
  private readonly _verifiedNoExactMatch = signal<string | null>(null);
  private _exactTitleLookupInProgress = false;
  private _exactTitleLookupId = 0;
  protected readonly searching = signal(false);
  protected readonly searchError = signal(false);
  protected readonly searchSucceeded = signal(false);
  protected readonly showDropdown = signal(false);
  protected readonly highlightedIndex = signal(-1);
  protected readonly selectedTitle = signal<{ id?: string; name: string } | null>(null);
  private readonly isFocused = signal(false);
  protected readonly hasNextPage = signal(false);
  protected readonly loadingMore = signal(false);
  protected readonly loadingMoreError = signal(false);
  private _endCursor = signal<string | undefined>(undefined);
  private _queryRef: QueryRef<GetRefTestTitlesQuery, GetRefTestTitlesQueryVariables> | null = null;

  private readonly _searchSubject = new Subject<string>();

  private readonly _searchResult = toSignal(
    this._searchSubject.pipe(
      debounceTime(300),
      switchMap((searchTerm: string): Observable<ISearchResult> => {
        const trimmedTerm = searchTerm.trim();
        const where = trimmedTerm.length > 0 ? { value: { contains: trimmedTerm } } : undefined;
        this.searching.set(true);
        this.searchError.set(false);
        this.searchSucceeded.set(false);

        this._queryRef = this._getRefTestTitlesGQL.watch({
          variables: {
            first: 10,
            where,
            order: { value: 'ASC' },
          },
          fetchPolicy: 'cache-first',
        });

        return this._queryRef.valueChanges.pipe(
          map((result): ISearchResult => {
            const edges = result.data?.refTestTitles?.edges ?? [];
            const pageInfo = result.data?.refTestTitles?.pageInfo;
            return {
              searchTerm: trimmedTerm,
              titles: edges
                .filter((edge) => !!edge?.node)
                .map((edge) => ({
                  id: edge!.node!.id!,
                  value: edge!.node!.value!,
                })),
              isSearching: result.loading,
              hasNextPage: pageInfo?.hasNextPage ?? false,
              endCursor: pageInfo?.endCursor ?? undefined,
              error: !!result.error || (!result.loading && !result.data?.refTestTitles),
            };
          }),
          catchError(() =>
            of<ISearchResult>({
              searchTerm: trimmedTerm,
              titles: [],
              isSearching: false,
              hasNextPage: false,
              error: true,
            }),
          ),
        );
      }),
    ),
    {
      initialValue: {
        searchTerm: '',
        titles: [],
        isSearching: false,
        hasNextPage: false,
        error: false,
      },
    },
  );

  constructor() {
    // Initialize from initialTitle input
    effect(() => {
      const initial = this.initialTitle();
      if (initial) {
        this.searchTerm.set(initial.name);
        this.selectedTitle.set(initial);
      }
    });

    effect(() => {
      const result = this._searchResult();
      if (!result) return;
      const currentTerm = this.searchTerm().trim();
      if (result.searchTerm !== currentTerm) return;
      if (this._exactTitleLookupInProgress) return;

      if (result.error) {
        this.suggestions.set([]);
        this.searching.set(false);
        this.searchError.set(true);
        this.searchSucceeded.set(false);
        this.hasNextPage.set(false);
        this.loadingMore.set(false);
        this.showDropdown.set(currentTerm.length > 0);
        return;
      }

      if (this.loadingMore()) {
        this.suggestions.update((current) => [...current, ...result.titles]);
        this.loadingMore.set(false);
      } else {
        this.suggestions.set(result.titles);
      }
      this.searching.set(result.isSearching);
      this.searchError.set(false);
      this.searchSucceeded.set(!result.isSearching);
      this.hasNextPage.set(result.hasNextPage);
      this._endCursor.set(result.endCursor);
      this.showDropdown.set(
        this.isFocused() &&
          (result.titles.length > 0 || result.isSearching || currentTerm.length > 0),
      );

      // Auto-select first suggestion when results arrive
      if (result.titles.length > 0 && !this.loadingMore()) {
        this.highlightedIndex.set(0);
      }

      if (this._pendingBlur() && !result.isSearching) {
        untracked(() => this.resolveBlur());
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
    this.loadingMoreError.set(false);
    this._pendingBlur.set(false);
    this._verifiedNoExactMatch.set(null);
    this.cancelExactTitleLookup();

    // Clear selection when user types - they're either searching or creating new
    const currentSelection = this.selectedTitle();
    if (currentSelection && currentSelection.name !== value) {
      this.selectedTitle.set(null);
    }
  }

  protected onFocus(): void {
    if (this._pendingBlur()) {
      this._pendingBlur.set(false);
      if (this._exactTitleLookupInProgress) {
        this.cancelExactTitleLookup();
        this.searching.set(false);
        this.searchSucceeded.set(true);
      }
    }
    this.isFocused.set(true);
    // Show dropdown immediately if we have suggestions
    if (this.suggestions().length > 0) {
      this.showDropdown.set(true);
    }

    this._searchSubject.next(this.searchTerm());
  }

  protected onKeyDown(event: KeyboardEvent): void {
    const suggestions = this.suggestions();

    if (event.key === 'Tab') {
      if (this.showDropdown() && suggestions.length > 0) {
        event.preventDefault();
        const index = this.highlightedIndex();
        if (index >= 0 && index < suggestions.length) {
          this.onSelectTitle(suggestions[index]);
        } else {
          this.onSelectTitle(suggestions[0]);
        }
        return;
      }

      // Dropdown not open — check for exact match in already-loaded suggestions
      const term = this.searchTerm().trim();
      if (term.length > 0 && !this.selectedTitle()) {
        const match = suggestions.find((s) => s.value.toLowerCase() === term.toLowerCase());
        if (match) {
          event.preventDefault();
          this.onSelectTitle(match);
          return;
        }
      }
      return;
    }

    if (
      event.key === 'Enter' &&
      (!this.showDropdown() || this.suggestions().length === 0)
    ) {
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
          current < suggestions.length - 1 ? current + 1 : 0,
        );
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.highlightedIndex.update((current) =>
          current > 0 ? current - 1 : suggestions.length - 1,
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

  protected onSelectTitle(title: ITitle): void {
    const selected = { id: title.id, name: title.value };
    this._pendingBlur.set(false);
    this._verifiedNoExactMatch.set(null);
    this.cancelExactTitleLookup();
    this.selectedTitle.set(selected);
    this.searchTerm.set(title.value);
    this.selectTitle.emit(selected);
    this.closeDropdown();
  }

  protected onManualEntry(): void {
    const term = this.searchTerm().trim();
    const matchingSuggestion = this.suggestions().find(
      (suggestion) => suggestion.value.toLowerCase() === term.toLowerCase(),
    );
    if (
      term.length === 0 ||
      this.searching() ||
      !this.searchSucceeded() ||
      this.searchError()
    ) {
      return;
    }

    if (matchingSuggestion) {
      this.onSelectTitle(matchingSuggestion);
      return;
    }

    if (this._verifiedNoExactMatch() !== term) {
      this.verifyExactTitleDoesNotExist(term);
      return;
    }

    this._pendingBlur.set(false);
    this._verifiedNoExactMatch.set(null);
    const selected = { name: term };
    this.selectedTitle.set(selected);
    this.selectTitle.emit(selected);
    this.closeDropdown();
  }

  protected onBlur(): void {
    this.isFocused.set(false);
    // Use setTimeout to ensure mousedown events on dropdown items complete first
    // and wait longer than the debounce time to allow search to complete
    setTimeout(() => {
      this._pendingBlur.set(true);
      this.resolveBlur();
    }, 400); // Increased from 150ms to 400ms to allow debounced search (300ms) to complete
  }

  protected onRetrySearch(): void {
    this.cancelExactTitleLookup();
    this._verifiedNoExactMatch.set(null);
    this.searching.set(true);
    this.searchError.set(false);
    this.searchSucceeded.set(false);
    this.showDropdown.set(true);
    this._searchSubject.next(this.searchTerm());
  }

  protected closeDropdown(): void {
    this.showDropdown.set(false);
    this.searching.set(false);
    this.highlightedIndex.set(-1);
  }

  protected onClear(): void {
    this._pendingBlur.set(false);
    this._verifiedNoExactMatch.set(null);
    this.cancelExactTitleLookup();
    this.searchTerm.set('');
    this.searching.set(false);
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
      this.loadMoreSuggestions();
    }
  }

  protected onRetryLoadMore(): void {
    this.loadMoreSuggestions();
  }

  private resolveBlur(): void {
    if (!this._pendingBlur()) return;
    if (this._exactTitleLookupInProgress) return;

    const term = this.searchTerm().trim();
    if (!term || this.selectedTitle()) {
      this._pendingBlur.set(false);
      this.closeDropdown();
      return;
    }

    if (this.searchError()) {
      this.showDropdown.set(true);
      return;
    }

    if (this.searching() || !this.searchSucceeded()) {
      if (!this.searching()) {
        this._pendingBlur.set(false);
        this.closeDropdown();
      }
      return;
    }

    const matchingSuggestion = this.suggestions().find(
      (suggestion) => suggestion.value.toLowerCase() === term.toLowerCase(),
    );
    if (matchingSuggestion) {
      this.onSelectTitle(matchingSuggestion);
    } else {
      this.onManualEntry();
    }
  }

  private verifyExactTitleDoesNotExist(term: string): void {
    const lookupId = ++this._exactTitleLookupId;
    this._exactTitleLookupInProgress = true;
    this.searching.set(true);
    this.searchSucceeded.set(false);
    this.searchError.set(false);
    this.showDropdown.set(true);

    // An equality lookup checks beyond the first page of contains suggestions.
    this._getRefTestTitlesGQL
      .fetch({
        variables: {
          first: 1,
          where: { value: { eq: term } },
          order: { value: 'ASC' },
        },
        fetchPolicy: 'network-only',
      })
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: (result) => {
          if (lookupId !== this._exactTitleLookupId || this.searchTerm().trim() !== term) {
            return;
          }
          const connection = result.data?.refTestTitles;
          if (result.error || !connection) {
            this.showExactTitleLookupError();
            return;
          }

          const exactTitle = connection.edges?.find(
            (edge) => edge?.node.value.toLowerCase() === term.toLowerCase(),
          )?.node;

          this._exactTitleLookupInProgress = false;
          this.searching.set(false);
          this.searchError.set(false);
          this.searchSucceeded.set(true);
          if (exactTitle) {
            this._verifiedNoExactMatch.set(null);
            this.onSelectTitle(exactTitle);
            return;
          }

          this._verifiedNoExactMatch.set(term);
          this.onManualEntry();
        },
        error: () => {
          if (lookupId === this._exactTitleLookupId && this.searchTerm().trim() === term) {
            this.showExactTitleLookupError();
          }
        },
      });
  }

  private showExactTitleLookupError(): void {
    this._exactTitleLookupInProgress = false;
    this.searching.set(false);
    this.searchError.set(true);
    this.searchSucceeded.set(false);
    this.suggestions.set([]);
    this.showDropdown.set(true);
  }

  private cancelExactTitleLookup(): void {
    this._exactTitleLookupId += 1;
    this._exactTitleLookupInProgress = false;
  }

  private loadMoreSuggestions(): void {
    if (!this._queryRef || this.loadingMore()) return;

    this.loadingMore.set(true);
    this.loadingMoreError.set(false);
    void this._queryRef
      .fetchMore({
        variables: {
          after: this._endCursor(),
        },
      })
      .catch(() => {
        this.loadingMore.set(false);
        this.loadingMoreError.set(true);
      });
  }
}
