import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { disabled, form, FormField, min, required } from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, map, tap } from 'rxjs';
import { UpdateRefTestConfigurationGQL } from '../../../../../../../../../graphql/generated';
import { Banner as BannerService } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';
import { toSnakeCase } from '../../../../../../../shared/utils/string-utils';
import { QuestionSearchAutocomplete } from '../../../../../../create/components/question-search-autocomplete/question-search-autocomplete';
import { TitleAutocomplete } from '../../../../../../create/components/title-autocomplete/title-autocomplete';

interface IConfigurationData {
  title: { id?: string; name: string } | null;
  numberOfQuestions: number;
  randomQuestionsForEachUser: boolean;
  maxTimeInMinutes: number;
}

@Component({
  selector: 'app-edit-configuration-dialog',
  imports: [TranslatePipe, FormField, TitleAutocomplete, QuestionSearchAutocomplete, Banner],
  templateUrl: './edit-configuration-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditConfigurationDialog {
  private readonly _updateRefTestConfigurationGQL = inject(UpdateRefTestConfigurationGQL);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _bannerServiceRoot = inject(BannerService);
  private readonly _translate = inject(TranslateService);

  // Create isolated banner manager for this dialog
  protected readonly bannerManager = this._bannerServiceRoot.createIsolated();

  protected readonly configurationModel = signal<IConfigurationData>({
    title: null,
    numberOfQuestions: 30,
    randomQuestionsForEachUser: false,
    maxTimeInMinutes: 60,
  });

  protected readonly configurationForm = form(this.configurationModel, (schema) => {
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

  protected readonly loading = signal(false);
  protected readonly selectedQuestions = signal<
    Array<{ number: string; phrase: Record<string, string> }>
  >([]);
  private readonly previousSelectedQuestions = signal<
    Array<{ number: string; phrase: Record<string, string> }>
  >([]);

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly closeDialog = output<void>();
  readonly cancel = output<void>();

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

  readonly currentLanguage = toSignal(
    this._translate.onLangChange.pipe(map(() => this._translate.getCurrentLang())),
    {
      initialValue: this._translate.getCurrentLang(),
    },
  );

  initialize(
    refTestId: string,
    title: { id?: string; value: string } | null,
    numberOfQuestions: number,
    maxTimeInMinutes: number,
    questions: { number: string; phrase: Record<string, string> }[],
  ): void {
    this.refTestId.set(refTestId);

    const titleValue = title ? { id: title.id, name: title.value } : null;
    this.configurationModel.set({
      title: titleValue,
      numberOfQuestions,
      randomQuestionsForEachUser: false,
      maxTimeInMinutes,
    });

    this.selectedQuestions.set(
      questions?.map((q) => ({ number: q.number, phrase: q.phrase })) || [],
    );
  }

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
          this.updateQuestionCount();
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

  protected onSave(): void {
    if (this.configurationForm().invalid()) {
      this.configurationForm().markAsTouched();
      return;
    }

    const data = this.configurationModel();

    const questionNumbers =
      this.selectedQuestions().length > 0 ? this.selectedQuestions().map((q) => q.number) : null;

    this._updateRefTestConfigurationGQL
      .mutate({
        variables: {
          input: {
            id: this.refTestId(),
            title: {
              id: data.title?.id,
              name: data.title?.id ? undefined : data.title?.name,
            },
            numberOfQuestions: questionNumbers ? questionNumbers.length : data.numberOfQuestions,
            maxTimeInMinutes: data.maxTimeInMinutes,
            randomQuestions: data.randomQuestionsForEachUser,
            specificQuestionNumbers: questionNumbers || undefined,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.updateRefTestConfiguration),
        tap((data) => {
          if (data?.errors && data.errors.length > 0) {
            const error = data.errors[0];
            if ('__typename' in error && error.__typename) {
              this.bannerManager.error(this._translate.instant(toSnakeCase(error.__typename)));
            }
            return;
          }

          if (data?.refTest) {
            this.bannerManager.success(
              this._translate.instant('ref_tests.detail.edit_configuration.success'),
            );
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.bannerManager.error(
            this._translate.instant('ref_tests.detail.edit_configuration.error'),
          );
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
