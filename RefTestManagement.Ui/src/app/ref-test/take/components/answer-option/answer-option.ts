import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslationPipe } from '../../../../pipes/translation-pipe';

@Component({
  selector: 'app-answer-option',
  templateUrl: './answer-option.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslationPipe],
  host: {
    class: 'block',
  },
})
export class AnswerOption {
  readonly phrase = input.required<Record<string, string>>();
  readonly selected = input.required<boolean>();
  readonly currentLanguage = input.required<string>();
  protected readonly answerClick = output<void>();
}
