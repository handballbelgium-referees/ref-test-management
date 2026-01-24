import { Injectable } from '@angular/core';
import {
  BooleanOperationFilterInput,
  DateTimeOperationFilterInput,
  FloatOperationFilterInput,
  IntOperationFilterInput,
  RefTestFilterInput,
  SortEnumType,
  StringOperationFilterInput,
} from '../../../../../graphql/generated';
import { PERCENTAGE_RANGES } from './constants';
import { IRefTestFilter } from './types';

@Injectable()
export class RefTestQueryBuilder {
  buildWhereFilter(
    filter: IRefTestFilter,
    options: { excludeStatus?: boolean } = {},
  ): RefTestFilterInput | undefined {
    const filters: RefTestFilterInput = {};

    // Search term filter (OR across email, firstName, lastName)
    if (filter.searchTerm) {
      const searchFilter: StringOperationFilterInput = {
        contains: filter.searchTerm,
      };
      filters.or = [
        { email: searchFilter },
        { firstName: searchFilter },
        { lastName: searchFilter },
      ];
    }

    // Status filter
    if (!options.excludeStatus && filter.status) {
      filters.status = { eq: filter.status };
    }

    // Title filter
    if (filter.titleValue) {
      filters.title = {
        value: { eq: filter.titleValue },
      };
    }

    // Boolean filters
    if (filter.invitationSent !== undefined) {
      filters.invitationSent = {
        eq: filter.invitationSent,
      } as BooleanOperationFilterInput;
    }

    if (filter.resultsSent !== undefined) {
      filters.resultsSent = {
        eq: filter.resultsSent,
      } as BooleanOperationFilterInput;
    }

    // Score filters
    this.applyIntRangeFilter(
      filters,
      'questionScore',
      filter.minQuestionScore,
      filter.maxQuestionScore,
    );

    this.applyIntRangeFilter(filters, 'answerScore', filter.minAnswerScore, filter.maxAnswerScore);

    // Percentage filter
    if (filter.percentageRange) {
      filters.percentage = this.buildPercentageFilter(filter.percentageRange);
    }

    // Questions count filter
    this.applyIntRangeFilter(
      filters,
      'numberOfQuestions',
      filter.minQuestions,
      filter.maxQuestions,
    );

    // Max time filter
    this.applyIntRangeFilter(
      filters,
      'maxTimeInMinutes',
      filter.minMaxTimeInMinutes,
      filter.maxMaxTimeInMinutes,
    );

    // Date filters
    this.applyDateRangeFilter(filters, 'startedAt', filter.startedAfter, filter.startedBefore);

    this.applyDateRangeFilter(
      filters,
      'completedAt',
      filter.completedAfter,
      filter.completedBefore,
    );

    return Object.keys(filters).length > 0 ? filters : undefined;
  }

  buildOrderClause(filter: IRefTestFilter) {
    // Special handling for participant sorting
    if (filter.sortField === 'email') {
      return [{ lastName: filter.sortDirection }, { firstName: filter.sortDirection }];
    }

    return [
      { [filter.sortField]: filter.sortDirection },
      { createdAt: SortEnumType.Desc }, // Secondary sort for consistency
    ];
  }

  mergeFilters(
    baseFilter: RefTestFilterInput | undefined,
    additionalFilter: Partial<RefTestFilterInput>,
  ): RefTestFilterInput {
    if (!baseFilter) {
      return additionalFilter as RefTestFilterInput;
    }

    return { ...baseFilter, ...additionalFilter };
  }

  private applyIntRangeFilter(
    filters: RefTestFilterInput,
    field: 'questionScore' | 'answerScore' | 'numberOfQuestions' | 'maxTimeInMinutes',
    min?: number,
    max?: number,
  ): void {
    if (min === undefined && max === undefined) {
      return;
    }

    const rangeFilter: IntOperationFilterInput = {};
    if (min !== undefined) rangeFilter.gte = min;
    if (max !== undefined) rangeFilter.lte = max;

    filters[field] = rangeFilter;
  }

  private applyDateRangeFilter(
    filters: RefTestFilterInput,
    field: 'startedAt' | 'completedAt',
    after?: string,
    before?: string,
  ): void {
    if (!after && !before) {
      return;
    }

    const dateFilter: DateTimeOperationFilterInput = {};
    if (after) dateFilter.gte = after;
    if (before) dateFilter.lte = before;

    filters[field] = dateFilter;
  }

  private buildPercentageFilter(range: 'low' | 'medium' | 'high'): FloatOperationFilterInput {
    const filter: FloatOperationFilterInput = {};

    switch (range) {
      case 'low':
        filter.lt = PERCENTAGE_RANGES.LOW.max;
        break;
      case 'medium':
        filter.gte = PERCENTAGE_RANGES.MEDIUM.min;
        filter.lt = PERCENTAGE_RANGES.MEDIUM.max;
        break;
      case 'high':
        filter.gte = PERCENTAGE_RANGES.HIGH.min;
        break;
    }

    return filter;
  }
}
