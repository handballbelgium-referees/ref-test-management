import { inject, Service } from '@angular/core';
import { RefTestStatus, SortEnumType } from '../../../../../graphql/generated';
import { RefTestFilterState } from './ref-test-filter-state';
import { SortField } from './types';

/**
 * Service responsible for handling filter actions and coordinating
 * filter state changes with pagination reset.
 */
@Service()
export class RefTestFilterActions {
  private readonly _filterState = inject(RefTestFilterState);

  // Callback to be set by the component for pagination reset
  private _onFilterChange?: () => void;

  /**
   * Set the callback to be invoked when any filter changes
   */
  setOnFilterChangeCallback(callback: () => void): void {
    this._onFilterChange = callback;
  }

  // ========================================================================
  // FILTER ACTIONS
  // ========================================================================

  setStatusFilter(status?: RefTestStatus): void {
    this._filterState.setStatus(status);
    this._onFilterChange?.();
  }

  setTitleFilter(titleId?: string): void {
    this._filterState.setTitle(titleId);
    this._onFilterChange?.();
  }

  setInvitationFilter(invitationSent?: boolean): void {
    this._filterState.setInvitationSent(invitationSent);
    this._onFilterChange?.();
  }

  setResultsFilter(resultsSent?: boolean): void {
    this._filterState.setResultsSent(resultsSent);
    this._onFilterChange?.();
  }

  setIsAnonymizedFilter(isAnonymized?: boolean): void {
    this._filterState.setIsAnonymized(isAnonymized);
    this._onFilterChange?.();
  }

  setLanguageFilter(language?: string): void {
    this._filterState.setLanguage(language);
    this._onFilterChange?.();
  }

  setSorting(sortField: SortField, sortDirection: SortEnumType): void {
    this._filterState.setSorting(sortField, sortDirection);
    this._onFilterChange?.();
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
    this._filterState.setPerformanceFilters(performance);
    this._onFilterChange?.();
  }

  setDateRange(type: 'started' | 'completed' | 'scheduled', after?: string, before?: string): void {
    this._filterState.setDateRange(type, after, before);
    this._onFilterChange?.();
  }

  sortByColumn(field: SortField): void {
    this._filterState.toggleSortDirection(field);
    this._onFilterChange?.();
  }

  setSearchTerm(searchTerm: string): void {
    this._filterState.setSearchTerm(searchTerm);
    this._onFilterChange?.();
  }

  // ========================================================================
  // UTILITY METHODS
  // ========================================================================

  getAriaSort(field: SortField): 'none' | 'ascending' | 'descending' {
    const filter = this._filterState.filter();
    if (filter.sortField !== field) {
      return 'none';
    }

    return filter.sortDirection === 'ASC' ? 'ascending' : 'descending';
  }
}
