import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

interface IPerformanceFilters {
  minScore?: number;
  maxScore?: number;
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
  readonly minScore = input<number | undefined>();
  readonly maxScore = input<number | undefined>();
  readonly percentageRange = input<'low' | 'medium' | 'high' | '' | undefined>();
  readonly minQuestions = input<number | undefined>();
  readonly maxQuestions = input<number | undefined>();

  readonly performanceChange = output<Partial<IPerformanceFilters>>();

  protected onMinScoreChange(value: string): void {
    this.performanceChange.emit({ minScore: value ? Number(value) : undefined });
  }

  protected onMaxScoreChange(value: string): void {
    this.performanceChange.emit({ maxScore: value ? Number(value) : undefined });
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
