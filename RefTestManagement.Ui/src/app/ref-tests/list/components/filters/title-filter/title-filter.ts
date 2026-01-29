import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { QueryRef } from 'apollo-angular';
import { catchError, debounceTime, map, Observable, of, Subject, switchMap } from 'rxjs';
import {
  GetRefTestTitlesGQL,
  GetRefTestTitlesQuery,
  GetRefTestTitlesQueryVariables,
  SortEnumType,
} from '../../../../../../../graphql/generated';

interface ITitle {
  id: string;
  value: string;
}

interface ITitleSearchResult {
  titles: ITitle[];
  isSearching: boolean;
  hasNextPage: boolean;
  endCursor?: string;
}

@Component({
  selector: 'app-title-filter',
  imports: [TranslatePipe],
  templateUrl: './title-filter.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class TitleFilter {
  private readonly _getRefTestTitlesGQL = inject(GetRefTestTitlesGQL);

  readonly selectedTitleId = input<string | undefined>();
  protected readonly titleChange = output<string | undefined>();

  protected readonly searchTerm = signal('');
  protected readonly suggestions = signal<ITitle[]>([]);
  protected readonly searching = signal(false);
  protected readonly showDropdown = signal(false);
  protected readonly hasNextPage = signal(false);
  protected readonly loadingMore = signal(false);
  private readonly _endCursor = signal<string | undefined>(undefined);
  private _queryRef: QueryRef<GetRefTestTitlesQuery, GetRefTestTitlesQueryVariables> | null = null;

  private readonly _searchSubject = new Subject<string>();

  private readonly _searchResult = toSignal(
    this._searchSubject.pipe(
      debounceTime(300),
      switchMap((searchTerm: string): Observable<ITitleSearchResult> => {
        const trimmedTerm = searchTerm.trim();
        const where = trimmedTerm.length > 0 ? { value: { contains: trimmedTerm } } : undefined;

        this._queryRef = this._getRefTestTitlesGQL.watch({
          variables: {
            first: 20,
            where,
            order: { value: SortEnumType.Asc },
          },
          fetchPolicy: 'cache-first',
        });

        return this._queryRef!.valueChanges.pipe(
          map((result): ITitleSearchResult => {
            const edges = result.data?.refTestTitles?.edges ?? [];
            const pageInfo = result.data?.refTestTitles?.pageInfo;
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
          catchError(() =>
            of<ITitleSearchResult>({
              titles: [],
              isSearching: false,
              hasNextPage: false,
              endCursor: undefined,
            }),
          ),
        );
      }),
    ),
    { initialValue: { titles: [], isSearching: false, hasNextPage: false, endCursor: undefined } },
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
      this.showDropdown.set(result.titles.length > 0 || this.searchTerm().trim().length > 0);
    });
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm.set(value);
    this._searchSubject.next(value);

    // If the user manually clears the input and there's a selected title, clear the filter
    if (value.trim() === '' && this.selectedTitleId()) {
      this.titleChange.emit(undefined);
    }
  }

  protected onFocus(): void {
    if (this.suggestions().length > 0) {
      this.showDropdown.set(true);
    }

    if (this._queryRef) {
      this._queryRef.refetch();
    } else {
      this._searchSubject.next(this.searchTerm());
    }
  }

  protected onBlur(): void {
    setTimeout(() => this.showDropdown.set(false), 200);
  }

  protected onSelect(value: string): void {
    this.searchTerm.set(value);
    this.showDropdown.set(false);
    this.titleChange.emit(value);
  }

  protected onClear(): void {
    this.searchTerm.set('');
    this.suggestions.set([]);
    this.showDropdown.set(false);
    this.titleChange.emit(undefined);
  }

  protected onScroll(event: Event): void {
    const target = event.target as HTMLElement;
    const threshold = 50;
    const atBottom = target.scrollHeight - target.scrollTop - target.clientHeight < threshold;

    if (atBottom && this.hasNextPage() && !this.loadingMore() && !this.searching()) {
      this.loadingMore.set(true);
      const endCursor = this._endCursor();
      if (this._queryRef && endCursor) {
        this._queryRef.fetchMore({
          variables: {
            after: endCursor,
          },
        });
      }
    }
  }
}
