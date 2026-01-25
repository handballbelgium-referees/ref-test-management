import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { map } from 'rxjs';
import { RefTestDetailDataService } from '../../services/ref-test-detail-data.service';
import { AnswersSummary } from './components/answers-summary/answers-summary';
import { EmptyQuestionsState } from './components/empty-questions-state/empty-questions-state';
import { QuestionCard } from './components/question-card/question-card';

@Component({
  selector: 'app-ref-test-questions-tab',
  imports: [EmptyQuestionsState, QuestionCard, AnswersSummary],
  templateUrl: './ref-test-questions-tab.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestQuestionsTab {
  private readonly _translate = inject(TranslateService);
  private readonly _dataService = inject(RefTestDetailDataService);

  protected readonly refTest = this._dataService.refTest;

  protected readonly currentLanguage = toSignal(
    this._translate.onLangChange.pipe(map(() => this._translate.getCurrentLang())),
    {
      initialValue: this._translate.getCurrentLang(),
    },
  );

  protected readonly selectedAnswerIdsSet = computed<Set<string>>(() => {
    const test = this.refTest();
    return test ? new Set<string>(test.selectedAnswerIds) : new Set<string>();
  });

  protected hasQuestions = computed(() => {
    const test = this.refTest();
    if (!test) return false;
    const questions = test.questions;
    return questions !== null && questions !== undefined && questions.length > 0;
  });

  protected readonly answeredQuestionsCount = computed(() => {
    const test = this.refTest();
    if (!test) return 0;
    const questions = test.questions ?? [];
    const selectedIds = this.selectedAnswerIdsSet();

    return questions.filter((question) => {
      if (!question?.answers) return false;
      return question.answers.some((answer) => selectedIds.has(answer.id));
    }).length;
  });
}
