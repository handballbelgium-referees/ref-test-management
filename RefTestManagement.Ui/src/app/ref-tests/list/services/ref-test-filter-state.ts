import { Service, signal } from '@angular/core';
import { RefTestStatus, SortEnumType } from '../../../../../graphql/generated';
import { REF_TEST_CONFIG } from './constants';
import { IRefTestFilter, SortField } from './types';

@Service()
export class RefTestFilterState {
  private readonly _filter = signal<IRefTestFilter>({
    searchTerm: '',
    sortField: 'completedAt',
    sortDirection: 'DESC',
    pagingInfo: { first: REF_TEST_CONFIG.PAGE_SIZE },
  });

  readonly filter = this._filter.asReadonly();

  updateFilter(partial: Partial<IRefTestFilter>): void {
    this._filter.update((current) => ({ ...current, ...partial }));
  }

  setStatus(status?: RefTestStatus): void {
    this.updateFilter({ status });
  }

  setTitle(titleValue?: string): void {
    this._filter.update((current) => {
      const updated = { ...current };
      if (titleValue) {
        updated.titleValue = titleValue;
      } else {
        delete updated.titleValue;
      }
      return updated;
    });
  }

  setInvitationSent(invitationSent?: boolean): void {
    this.updateFilter({ invitationSent });
  }

  setResultsSent(resultsSent?: boolean): void {
    this.updateFilter({ resultsSent });
  }

  setLanguage(language?: string): void {
    this._filter.update((current) => {
      const updated = { ...current };
      if (language) {
        updated.language = language;
      } else {
        delete updated.language;
      }
      return updated;
    });
  }

  setSearchTerm(searchTerm: string): void {
    this.updateFilter({ searchTerm });
  }

  setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this.updateFilter({ sortField, sortDirection });
  }

  setEndCursor(endCursor?: string): void {
    this.updateFilter({
      pagingInfo: {
        first: REF_TEST_CONFIG.PAGE_SIZE,
        after: endCursor,
      },
    });
  }

  toggleSortDirection(field: SortField): void {
    const current = this._filter();
    if (current.sortField === field) {
      const newDirection = current.sortDirection === 'ASC' ? 'DESC' : 'ASC';
      this.setSorting(field, newDirection);
    } else {
      this.setSorting(field, 'DESC');
    }
  }

  setPerformanceFilters(performance: {
    minQuestionScore?: number;
    maxQuestionScore?: number;
    minAnswerScore?: number;
    maxAnswerScore?: number;
    percentageRange?: 'low' | 'medium' | 'high';
    minQuestions?: number;
    maxQuestions?: number;
    minMaxTimeInMinutes?: number;
    maxMaxTimeInMinutes?: number;
  }): void {
    this.updateFilter(performance);
  }

  setDateRange(type: 'started' | 'completed' | 'scheduled', after?: string, before?: string): void {
    if (type === 'started') {
      this.updateFilter({ startedAfter: after, startedBefore: before });
    } else if (type === 'completed') {
      this.updateFilter({ completedAfter: after, completedBefore: before });
    } else {
      this.updateFilter({ scheduledAfter: after, scheduledBefore: before });
    }
  }

  reset(): void {
    this._filter.set({
      searchTerm: '',
      sortField: 'completedAt',
      sortDirection: 'DESC',
      pagingInfo: { first: REF_TEST_CONFIG.PAGE_SIZE },
    });
  }
}
