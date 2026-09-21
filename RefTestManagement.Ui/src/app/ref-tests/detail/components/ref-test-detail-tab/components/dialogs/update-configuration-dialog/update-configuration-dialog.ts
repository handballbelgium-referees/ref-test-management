import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { disabled, form, FormField, min, required } from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  GetRefTestByIdQuery,
  UpdateRefTestConfigurationInput,
} from '../../../../../../../../../graphql/generated';
import { IsolatedBannerManager } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';
import { Dialog } from '../../../../../../../shared/components/dialog/dialog';
import { QuestionSearchAutocomplete } from '../../../../../../create/components/question-search-autocomplete/question-search-autocomplete';
import { TitleAutocomplete } from '../../../../../../create/components/title-autocomplete/title-autocomplete';

type RefTest = Extract<GetRefTestByIdQuery['refTest'], { __typename: 'RefTest' }>;

interface IConfigurationData {
  id: string;
  title: { id?: string; name: string } | null;
  numberOfQuestions: number;
  randomQuestionsForEachUser: boolean;
  maxTimeInMinutes: number;
}

@Component({
  selector: 'app-update-configuration-dialog',
  imports: [
    TranslatePipe,
    FormField,
    TitleAutocomplete,
    QuestionSearchAutocomplete,
    Banner,
    Dialog,
  ],
  templateUrl: './update-configuration-dialog.html',
})
export class UpdateConfigurationDialog {
  private readonly _translate = inject(TranslateService);

  protected readonly configurationModel = signal<IConfigurationData>({
    id: '',
    title: null,
    numberOfQuestions: 30,
    randomQuestionsForEachUser: false,
    maxTimeInMinutes: 60,
  });

  protected readonly configurationForm = form(this.configurationModel, (schema) => {
    required(schema.id);
    required(schema.title, {
      message: 'ref_tests.detail.edit_configuration.title_required',
    });
    required(schema.numberOfQuestions, {
      message: 'ref_tests.detail.edit_configuration.number_of_questions_required',
      when: () => this.selectedQuestions().length === 0,
    });
    disabled(schema.numberOfQuestions, () => this.selectedQuestions().length > 0);
    min(schema.numberOfQuestions, 1, {
      message: 'ref_tests.detail.edit_configuration.number_of_questions_min',
    });
    required(schema.maxTimeInMinutes, {
      message: 'ref_tests.detail.edit_configuration.max_time_required',
    });
    min(schema.maxTimeInMinutes, 1, {
      message: 'ref_tests.detail.edit_configuration.max_time_min',
    });
  });

  protected readonly selectedQuestions = signal<
    Array<{ number: string; phrase: Record<string, string> }>
  >([]);
  private readonly previousSelectedQuestions = signal<
    Array<{ number: string; phrase: Record<string, string> }>
  >([]);

  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly initialData = input.required<RefTest | undefined>();
  readonly bannerManager = input.required<IsolatedBannerManager>();
  protected readonly confirm = output<UpdateRefTestConfigurationInput>();
  protected readonly cancel = output<void>();

  protected readonly canSave = computed(() => {
    return this.configurationForm().valid() && !this.loading();
  });

  protected readonly specificQuestionNumbers = computed(() => {
    return this.selectedQuestions()
      .map((q) => q.number)
      .join(', ');
  });

  protected readonly titleValue = computed(() => {
    return this.configurationModel().title?.name || '';
  });

  protected readonly currentLanguage = this._translate.currentLang;

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });

    // Save and restore selected questions when randomQuestionsForEachUser is toggled
    effect(() => {
      const config = this.configurationModel();
      const currentSelected = this.selectedQuestions();

      if (config.randomQuestionsForEachUser) {
        // Checkbox is checked - save current questions and clear
        if (currentSelected.length > 0) {
          this.previousSelectedQuestions.set(currentSelected);
          this.selectedQuestions.set([]);
        }
      } else {
        // Checkbox is unchecked - restore previous questions
        const previous = this.previousSelectedQuestions();
        if (previous.length > 0 && currentSelected.length === 0) {
          this.selectedQuestions.set(previous);
          this.updateQuestionCount();
        }
      }
    });

    effect(() => {
      const initial = this.initialData();
      if (!initial) return;
      const titleValue = initial.title ? { id: initial.title.id, name: initial.title.value } : null;
      this.configurationModel.set({
        id: initial.id,
        title: titleValue,
        numberOfQuestions: initial.numberOfQuestions,
        randomQuestionsForEachUser: false,
        maxTimeInMinutes: initial.maxTimeInMinutes,
      });

      this.selectedQuestions.set(
        initial.questions
          ?.filter((q) => !!q)
          .map((q) => ({
            number: q.number,
            phrase: q.phrase as Record<string, string>,
          })) ?? [],
      );
    });
  }

  protected onQuestionSelect(question: { number: string; phrase: Record<string, string> }): void {
    const current = this.selectedQuestions();
    if (!current.some((q) => q.number === question.number)) {
      this.selectedQuestions.set([...current, question]);
      this.updateQuestionCount();
    }
  }

  protected onQuestionRemove(questionNumber: string): void {
    this.selectedQuestions.update((current) => current.filter((q) => q.number !== questionNumber));
    this.updateQuestionCount();
  }

  private updateQuestionCount(): void {
    const count = this.selectedQuestions().length;
    const current = this.configurationModel();
    this.configurationModel.set({
      ...current,
      numberOfQuestions: count > 0 ? count : 30,
    });
  }

  protected onTitleSelect(title: { id?: string; name: string }): void {
    const current = this.configurationModel();
    this.configurationModel.set({
      ...current,
      title: title.name ? title : null,
    });
  }

  protected onConfirm(): void {
    if (this.configurationForm().invalid()) {
      this.configurationForm().markAsTouched();
      return;
    }

    const data = this.configurationModel();

    const questionNumbers =
      this.selectedQuestions().length > 0 ? this.selectedQuestions().map((q) => q.number) : null;

    this.confirm.emit({
      id: data.id,
      title: {
        id: data.title?.id,
        name: data.title?.id ? undefined : data.title?.name,
      },
      numberOfQuestions: questionNumbers ? questionNumbers.length : data.numberOfQuestions,
      maxTimeInMinutes: data.maxTimeInMinutes,
      randomQuestions: data.randomQuestionsForEachUser,
      specificQuestionNumbers: questionNumbers || undefined,
    });
  }
}
