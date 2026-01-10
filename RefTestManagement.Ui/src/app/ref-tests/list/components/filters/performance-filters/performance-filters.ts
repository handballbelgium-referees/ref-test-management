import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IPerformanceFilters {
  minQuestionScore?: number;
  maxQuestionScore?: number;
  minAnswerScore?: number;
  maxAnswerScore?: number;
  percentageRange?: 'low' | 'medium' | 'high' | '';
  minQuestions?: number;
  maxQuestions?: number;
}

@Component({
  selector: 'app-performance-filters',
  imports: [TranslatePipe],
  templateUrl: './performance-filters.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class PerformanceFilters {
  readonly minQuestionScore = input<number | undefined>();
  readonly maxQuestionScore = input<number | undefined>();
  readonly minAnswerScore = input<number | undefined>();
  readonly maxAnswerScore = input<number | undefined>();
  readonly percentageRange = input<'low' | 'medium' | 'high' | '' | undefined>();
  readonly minQuestions = input<number | undefined>();
  readonly maxQuestions = input<number | undefined>();

  readonly performanceChange = output<Partial<IPerformanceFilters>>();

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
}
