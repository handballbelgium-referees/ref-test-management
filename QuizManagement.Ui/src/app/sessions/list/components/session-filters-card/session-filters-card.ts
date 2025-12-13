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
  GetQuizTitlesGQL,
  GetQuizTitlesQuery,
  GetQuizTitlesQueryVariables,
  QuizSessionStatus,
  SortEnumType,
} from '../../../../../../graphql/generated';

type SortField =
  | 'title'
  | 'completedAt'
  | 'startedAt'
  | 'email'
  | 'score'
  | 'percentage'
  | 'status'
  | 'numberOfQuestions'
  | 'invitationSent';

interface Title {
  id: string;
  value: string;
}

interface TitleSearchResult {
  titles: Title[];
  isSearching: boolean;
  hasNextPage: boolean;
  endCursor?: string;
}

interface SessionFilter {
  status?: QuizSessionStatus;
  invitationSent?: boolean;
  titleId?: string;
  searchTerm: string;
  sortField: SortField;
  sortDirection: SortEnumType;
  minScore?: number;
  maxScore?: number;
  percentageRange?: 'low' | 'medium' | 'high';
  minQuestions?: number;
  maxQuestions?: number;
  startedAfter?: string;
  startedBefore?: string;
  completedAfter?: string;
  completedBefore?: string;
}

interface StatusCounts {
  all: number;
  pending: number;
  inProgress: number;
  completed: number;
  expired: number;
}

@Component({
  selector: 'app-session-filters-card',
  imports: [TranslatePipe],
  templateUrl: './session-filters-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    select {
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
      background-position: right 0.5rem center;
      background-repeat: no-repeat;
      background-size: 1.5em 1.5em;
    }
  `,
})
export class SessionFiltersCard {
  private readonly _getQuizTitlesGQL = inject(GetQuizTitlesGQL);

  readonly filter = input.required<SessionFilter>();
  readonly statusCounts = input.required<StatusCounts>();

  readonly statusFilterChange = output<QuizSessionStatus | undefined>();
  readonly titleFilterChange = output<string | undefined>();
  readonly invitationFilterChange = output<boolean | undefined>();

  protected readonly titleSearchTerm = signal('');
  protected readonly titleSuggestions = signal<Title[]>([]);
  protected readonly titleSearching = signal(false);
  protected readonly showTitleDropdown = signal(false);
  protected readonly titleHasNextPage = signal(false);
  protected readonly titleLoadingMore = signal(false);
  private _titleEndCursor = signal<string | undefined>(undefined);
  private _titleQueryRef: QueryRef<GetQuizTitlesQuery, GetQuizTitlesQueryVariables> | null = null;

  private readonly _titleSearchSubject = new Subject<string>();

  private readonly _titleSearchResult = toSignal(
    this._titleSearchSubject.pipe(
      debounceTime(300),
      switchMap((searchTerm: string): Observable<TitleSearchResult> => {
        const trimmedTerm = searchTerm.trim();
        const where = trimmedTerm.length > 0 ? { value: { contains: trimmedTerm } } : undefined;

        this._titleQueryRef = this._getQuizTitlesGQL.watch({
          variables: {
            first: 20,
            where,
            order: { value: SortEnumType.Asc },
          },
          fetchPolicy: 'cache-first',
        });

        return this._titleQueryRef!.valueChanges.pipe(
          map((result): TitleSearchResult => {
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
          catchError(() =>
            of<TitleSearchResult>({
              titles: [],
              isSearching: false,
              hasNextPage: false,
              endCursor: undefined,
            })
          )
        );
      })
    ),
    { initialValue: { titles: [], isSearching: false, hasNextPage: false, endCursor: undefined } }
  );
  readonly sortingChange = output<{ field: SortField; direction: SortEnumType }>();
  readonly scoreRangeChange = output<{ min?: number; max?: number }>();
  readonly percentageRangeChange = output<'low' | 'medium' | 'high' | undefined>();
  readonly questionsRangeChange = output<{ min?: number; max?: number }>();
  readonly dateRangeChange = output<{
    type: 'started' | 'completed';
    after?: string;
    before?: string;
  }>();

  protected readonly QuizSessionStatus = QuizSessionStatus;
  protected readonly SortEnumType = SortEnumType;

  protected readonly filtersExpanded = signal(false);
  protected readonly sortingExpanded = signal(false);

  constructor() {
    effect(() => {
      const result = this._titleSearchResult();
      if (!result) return;

      if (this.titleLoadingMore()) {
        this.titleSuggestions.update((current) => [...current, ...result.titles]);
        this.titleLoadingMore.set(false);
      } else {
        this.titleSuggestions.set(result.titles);
      }
      this.titleSearching.set(result.isSearching);
      this.titleHasNextPage.set(result.hasNextPage);
      this._titleEndCursor.set(result.endCursor);
      this.showTitleDropdown.set(
        (result.titles.length > 0 || this.titleSearchTerm().trim().length > 0) &&
          this.filtersExpanded()
      );
    });
  }

  protected toggleFilters(): void {
    this.filtersExpanded.update((v) => !v);
  }

  protected toggleSorting(): void {
    this.sortingExpanded.update((v) => !v);
  }

  protected onStatusChange(status?: QuizSessionStatus): void {
    this.statusFilterChange.emit(status);
  }

  protected onTitleSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.titleSearchTerm.set(value);
    this._titleSearchSubject.next(value);
  }

  protected onTitleFocus(): void {
    // Show dropdown immediately if we have suggestions
    if (this.titleSuggestions().length > 0) {
      this.showTitleDropdown.set(true);
    }

    // If we have a query ref, refetch immediately without debounce
    if (this._titleQueryRef) {
      this._titleQueryRef.refetch();
    } else {
      // First time - trigger search (will have debounce delay)
      this._titleSearchSubject.next(this.titleSearchTerm());
    }
  }

  protected onTitleBlur(): void {
    this.showTitleDropdown.set(false);
  }

  protected onTitleSelect(titleId: string, titleValue: string): void {
    this.titleSearchTerm.set(titleValue);
    this.titleFilterChange.emit(titleId);
    this.showTitleDropdown.set(false);
  }

  protected onTitleClear(): void {
    this.titleSearchTerm.set('');
    this.titleSuggestions.set([]);
    this.titleFilterChange.emit(undefined);
    this.showTitleDropdown.set(false);
  }

  protected onTitleScroll(event: Event): void {
    const element = event.target as HTMLElement;
    const atBottom = element.scrollHeight - element.scrollTop <= element.clientHeight + 50;

    if (
      atBottom &&
      this.titleHasNextPage() &&
      !this.titleLoadingMore() &&
      !this.titleSearching() &&
      this._titleQueryRef
    ) {
      this.titleLoadingMore.set(true);
      this._titleQueryRef.fetchMore({
        variables: {
          after: this._titleEndCursor(),
        },
      });
    }
  }

  protected onInvitationChange(value: string): void {
    this.invitationFilterChange.emit(value === '' ? undefined : value === 'true');
  }

  protected onSortFieldChange(field: string): void {
    this.sortingChange.emit({
      field: field as SortField,
      direction: this.filter().sortDirection,
    });
  }

  protected onSortDirectionChange(direction: string): void {
    this.sortingChange.emit({
      field: this.filter().sortField,
      direction: direction as SortEnumType,
    });
  }

  protected onMinScoreChange(value: string): void {
    const min = value ? Number(value) : undefined;
    this.scoreRangeChange.emit({ min, max: this.filter().maxScore });
  }

  protected onMaxScoreChange(value: string): void {
    const max = value ? Number(value) : undefined;
    this.scoreRangeChange.emit({ min: this.filter().minScore, max });
  }

  protected onPercentageRangeChange(value: string): void {
    this.percentageRangeChange.emit(
      value === '' ? undefined : (value as 'low' | 'medium' | 'high')
    );
  }

  protected onMinQuestionsChange(value: string): void {
    const min = value ? Number(value) : undefined;
    this.questionsRangeChange.emit({ min, max: this.filter().maxQuestions });
  }

  protected onMaxQuestionsChange(value: string): void {
    const max = value ? Number(value) : undefined;
    this.questionsRangeChange.emit({ min: this.filter().minQuestions, max });
  }

  protected onStartedAfterChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'started',
      after: value || undefined,
      before: this.filter().startedBefore,
    });
  }

  protected onStartedBeforeChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'started',
      after: this.filter().startedAfter,
      before: value || undefined,
    });
  }

  protected onCompletedAfterChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'completed',
      after: value || undefined,
      before: this.filter().completedBefore,
    });
  }

  protected onCompletedBeforeChange(value: string): void {
    this.dateRangeChange.emit({
      type: 'completed',
      after: this.filter().completedAfter,
      before: value || undefined,
    });
  }
}
