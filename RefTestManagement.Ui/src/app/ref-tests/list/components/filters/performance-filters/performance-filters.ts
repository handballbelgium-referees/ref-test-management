import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IPerformanceFilters {
  minQuestionScore?: number;
  maxQuestionScore?: number;
  minAnswerScore?: number;
  maxAnswerScore?: number;
  percentageRange?: 'low' | 'medium' | 'high' | '';
  minQuestions?: number;
  maxQuestions?: number;
  minMaxTimeInMinutes?: number;
  maxMaxTimeInMinutes?: number;
}

@Component({
  selector: 'app-performance-filters',
  imports: [TranslatePipe],
  templateUrl: './performance-filters.html',
  host: {
    class: 'block',
  },
  styles: `
    select {
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%236b7280' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
      background-position: right 0.5rem center;
      background-repeat: no-repeat;
      background-size: 1.5em 1.5em;
    }
  `,
})
export class PerformanceFilters {
  readonly minQuestionScore = input<number | undefined>();
  readonly maxQuestionScore = input<number | undefined>();
  readonly minAnswerScore = input<number | undefined>();
  readonly maxAnswerScore = input<number | undefined>();
  readonly percentageRange = input<'low' | 'medium' | 'high' | '' | undefined>();
  readonly minQuestions = input<number | undefined>();
  readonly maxQuestions = input<number | undefined>();
  readonly minMaxTimeInMinutes = input<number | undefined>();
  readonly maxMaxTimeInMinutes = input<number | undefined>();

  protected readonly performanceChange = output<Partial<IPerformanceFilters>>();

  protected onMinQuestionScoreChange(value: string): void {
    this.performanceChange.emit({ minQuestionScore: value ? Number(value) : undefined });
  }

  protected onMaxQuestionScoreChange(value: string): void {
    this.performanceChange.emit({ maxQuestionScore: value ? Number(value) : undefined });
  }

  protected onMinAnswerScoreChange(value: string): void {
    this.performanceChange.emit({ minAnswerScore: value ? Number(value) : undefined });
  }

  protected onMaxAnswerScoreChange(value: string): void {
    this.performanceChange.emit({ maxAnswerScore: value ? Number(value) : undefined });
  }

  protected onPercentageRangeChange(value: string): void {
    this.performanceChange.emit({
      percentageRange: value as 'low' | 'medium' | 'high' | '',
    });
  }

  protected onMinQuestionsChange(value: string): void {
    this.performanceChange.emit({ minQuestions: value ? Number(value) : undefined });
  }

  protected onMaxQuestionsChange(value: string): void {
    this.performanceChange.emit({ maxQuestions: value ? Number(value) : undefined });
  }

  protected onMinMaxTimeInMinutesChange(value: string): void {
    this.performanceChange.emit({ minMaxTimeInMinutes: value ? Number(value) : undefined });
  }

  protected onMaxMaxTimeInMinutesChange(value: string): void {
    this.performanceChange.emit({ maxMaxTimeInMinutes: value ? Number(value) : undefined });
  }
}
