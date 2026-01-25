import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { applyEach, disabled, email, form, FormField, min, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, delay, map, of, tap } from 'rxjs';
import { CreateRefTestsGQL, GetQuestionsByNumberGQL } from '../../../../graphql/generated';
import { Banner } from '../../services/banner';
import { QuestionImportModal } from './components/question-import-modal/question-import-modal';
import { QuestionSearchAutocomplete } from './components/question-search-autocomplete/question-search-autocomplete';
import { RefTestUserListItem } from './components/ref-test-user-list-item/ref-test-user-list-item';
import { TitleAutocomplete } from './components/title-autocomplete/title-autocomplete';
import { UserImportModal } from './components/user-import-modal/user-import-modal';

interface IUserData {
  firstName: string;
  lastName: string;
  email: string;
}

interface IRefTestFormData {
  users: IUserData[];
  title: { id?: string; name: string } | null;
  numberOfQuestions: number;
  randomQuestionsForEachUser: boolean;
  maxTimeInMinutes: number;
  specificQuestionNumbers: string;
  sendInvitations: boolean;
  sendResults: boolean;
}

@Component({
  selector: 'app-create-ref-tests',
  imports: [
    TranslatePipe,
    FormField,
    RefTestUserListItem,
    UserImportModal,
    QuestionSearchAutocomplete,
    QuestionImportModal,
    TitleAutocomplete,
  ],
  templateUrl: './create-ref-tests.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class CreateRefTests {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _createRefTestsGQL = inject(CreateRefTestsGQL);
  private readonly _getQuestionsByNumberGQL = inject(GetQuestionsByNumberGQL);
  private readonly _router = inject(Router);
  private readonly _translate = inject(TranslateService);
  private readonly _bannerService = inject(Banner);

  protected readonly messagesContainer = viewChild<ElementRef>('messagesContainer');

  // Angular v21 Signal Forms - model signal
  protected readonly refTestModel = signal<IRefTestFormData>({
    users: [{ firstName: '', lastName: '', email: '' }],
    title: null,
    numberOfQuestions: 30,
    randomQuestionsForEachUser: false,
    maxTimeInMinutes: 60,
    specificQuestionNumbers: '',
    sendInvitations: false,
    sendResults: false,
  });

  // Form field tree with validation schema
  protected readonly refTestForm = form(this.refTestModel, (schemaPath) => {
    // Validate each user in the array
    applyEach(schemaPath.users, (user) => {
      required(user.firstName, {
        message: 'ref_tests.create.form.users.first_name_required',
      });
      required(user.lastName, {
        message: 'ref_tests.create.form.users.last_name_required',
      });
      required(user.email, {
        message: 'ref_tests.create.form.users.email_required',
      });
      email(user.email, {
        message: 'ref_tests.create.form.users.email_invalid',
      });
    });

    // Validate title
    required(schemaPath.title, {
      message: 'ref_tests.create.form.title.required',
    });

    // Validate refTest configuration
    required(schemaPath.numberOfQuestions, {
      message: 'ref_tests.create.form.number_of_questions.required',
      when: () => {
        return this.selectedQuestions().length === 0;
      },
    });
    disabled(schemaPath.numberOfQuestions, () => {
      return this.selectedQuestions().length > 0;
    });
    disabled(schemaPath.randomQuestionsForEachUser, () => {
      return this.selectedQuestions().length > 0;
    });
    min(schemaPath.numberOfQuestions, 1, {
      message: 'ref_tests.create.form.number_of_questions.min',
    });

    required(schemaPath.maxTimeInMinutes, {
      message: 'ref_tests.create.form.max_time_in_minutes.required',
    });
    min(schemaPath.maxTimeInMinutes, 1, {
      message: 'ref_tests.create.form.max_time_in_minutes.min',
    });
  });

  // Additional state signals
  protected readonly loading = signal(false);
  protected readonly showImport = signal(false);
  protected readonly selectedQuestions = signal<
    Array<{ number: string; phrase: Record<string, string> }>
  >([]);
  protected readonly showQuestionImport = signal(false);
  protected readonly loadingQuestions = signal(false);
  readonly currentLanguage = toSignal(
    this._translate.onLangChange.pipe(map(() => this._translate.getCurrentLang())),
    {
      initialValue: this._translate.getCurrentLang(),
    },
  );

  // Computed signals
  protected readonly userCount = computed(() => this.refTestModel().users.length);

  protected addUser(): void {
    const current = this.refTestModel();
    this.refTestModel.set({
      ...current,
      users: [...current.users, { firstName: '', lastName: '', email: '' }],
    });
  }

  protected removeUser(index: number): void {
    const current = this.refTestModel();
    const newUsers = current.users.filter((_, i) => i !== index);

    // Ensure at least one user remains
    if (newUsers.length === 0) {
      newUsers.push({ firstName: '', lastName: '', email: '' });
    }

    this.refTestModel.set({
      ...current,
      users: newUsers,
    });
  }

  protected toggleImport(): void {
    this.showImport.update((v) => !v);
  }

  protected toggleQuestionImport(): void {
    this.showQuestionImport.update((v) => !v);
  }

  protected onTitleSelect(title: { id?: string; name: string }): void {
    const current = this.refTestModel();
    this.refTestModel.set({
      ...current,
      title: title.name ? title : null,
    });
  }

  protected onQuestionSelect(question: { number: string; phrase: Record<string, string> }): void {
    const current = this.selectedQuestions();
    if (!current.some((q) => q.number === question.number)) {
      this.selectedQuestions.set([...current, question]);
      this.updateQuestionNumbersField();
      this.updateRandomQuestionsForEachUserField();
      this.resetQuestionCount();
    }
  }

  protected onQuestionRemove(questionNumber: string): void {
    this.selectedQuestions.update((current) => current.filter((q) => q.number !== questionNumber));
    this.updateQuestionNumbersField();
    this.resetQuestionCount();
  }

  private updateQuestionNumbersField(): void {
    const numbers = this.selectedQuestions()
      .map((q) => q.number)
      .join(', ');
    const current = this.refTestModel();
    this.refTestModel.set({
      ...current,
      specificQuestionNumbers: numbers,
    });
  }

  private updateRandomQuestionsForEachUserField(): void {
    const current = this.refTestModel();
    this.refTestModel.set({
      ...current,
      randomQuestionsForEachUser: false,
    });
  }

  private resetQuestionCount(): void {
    const count = this.selectedQuestions().length;
    const current = this.refTestModel();
    this.refTestModel.set({
      ...current,
      numberOfQuestions: count > 0 ? count : 30,
    });
  }

  protected onQuestionImport(text: string): void {
    const lines = text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    const questionNumbers = lines
      .flatMap((line) => line.split(','))
      .map((num) => num.trim())
      .filter((num) => num.length > 0);

    if (questionNumbers.length === 0) {
      this.showQuestionImport.set(false);
      return;
    }

    this.loadingQuestions.set(true);

    // Validate and add questions
    this._getQuestionsByNumberGQL
      .fetch({ variables: { numbers: questionNumbers } })
      .pipe(
        map((result) => {
          const questions = result.data?.questionsByNumber ?? [];
          return questions
            .filter((q) => q.phrase != null)
            .map((q) => ({ number: q.number, phrase: q.phrase! }))
            .filter((q) => !this.selectedQuestions().some((sq) => sq.number === q.number));
        }),
        tap((validQuestions) => {
          if (validQuestions.length > 0) {
            this.selectedQuestions.update((current) => [...current, ...validQuestions]);
            this.updateQuestionNumbersField();
          }
        }),
        tap(() => {
          this.loadingQuestions.set(false);
          this.showQuestionImport.set(false);
          this.resetQuestionCount();
        }),
        catchError(() => {
          this.loadingQuestions.set(false);
          return of([]);
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected onUserImport(text: string): void {
    const lines = text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    const users: IUserData[] = [];

    lines.forEach((line) => {
      const parts = line.split(/\s+/);
      let firstName = '';
      let lastName = '';
      let email = '';

      if (parts.length >= 3) {
        email = parts[parts.length - 1];
        lastName = parts[parts.length - 2];
        firstName = parts.slice(0, parts.length - 2).join(' ');
      } else if (parts.length === 1 && parts[0].includes('@')) {
        email = parts[0];
        const [localPart] = email.split('@');
        firstName = localPart;
      }

      if (email) {
        users.push({ firstName, lastName, email });
      }
    });

    if (users.length > 0) {
      const current = this.refTestModel();
      // Filter out empty users (all fields empty)
      const nonEmptyUsers = current.users.filter(
        (user) => user.firstName.trim() || user.lastName.trim() || user.email.trim(),
      );
      this.refTestModel.set({
        ...current,
        users: [...nonEmptyUsers, ...users],
      });
    }

    this.showImport.set(false);
  }

  protected onUserCancel(): void {
    this.showImport.set(false);
  }

  protected onQuestionCancel(): void {
    this.showQuestionImport.set(false);
  }

  protected onSubmit(): void {
    // Check form validity
    if (this.refTestForm().invalid()) {
      // Mark all fields as touched to reveal validation errors
      this.refTestForm().markAsTouched();
      this._bannerService.error(this._translate.instant('ref_tests.create.form.validation_error'));
      return;
    }

    const formData = this.refTestModel();

    if (formData.users.length === 0) {
      this._bannerService.error(this._translate.instant('ref_tests.create.form.no_participants'));
      return;
    }

    const specificQuestions = formData.specificQuestionNumbers
      .split(',')
      .map((q) => q.trim())
      .filter((q) => q.length > 0);

    this._createRefTestsGQL
      .mutate({
        variables: {
          input: {
            users: formData.users,
            title: formData.title?.id
              ? { id: formData.title.id }
              : { name: formData.title?.name ?? '' },
            numberOfQuestions: formData.numberOfQuestions,
            randomQuestionsForEachUser: formData.randomQuestionsForEachUser,
            maxTimeInMinutes: formData.maxTimeInMinutes,
            specificQuestionNumbers: specificQuestions.length > 0 ? specificQuestions : undefined,
            sendAutomatedInvitations: formData.sendInvitations,
            sendAutomatedResults: formData.sendResults,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.createRefTests?.createRefTestsResult),
        tap((data) => {
          if (data) {
            if (data.errors.length > 0) {
              // Show error banner with failed count
              this._bannerService.error(
                this._translate.instant('ref_tests.create.success.failed_info', {
                  count: data.failed,
                }),
              );
            }

            if (data.successfullyCreated > 0) {
              // Show success banner
              const successKey =
                data.successfullyCreated === 1
                  ? 'ref_tests.create.success.created_one'
                  : 'ref_tests.create.success.created_other';
              this._bannerService.success(
                this._translate.instant(successKey, { count: data.successfullyCreated }),
              );

              // Reset form to initial state
              this.refTestModel.set({
                users: [{ firstName: '', lastName: '', email: '' }],
                title: null,
                numberOfQuestions: 30,
                randomQuestionsForEachUser: false,
                maxTimeInMinutes: 60,
                specificQuestionNumbers: '',
                sendInvitations: false,
                sendResults: false,
              });
              // Reset form state
              this.refTestForm().reset();
            }

            if (data.successfullyCreated > 0 && data.failed === 0) {
              of(null)
                .pipe(delay(2000), takeUntilDestroyed(this._destroyRef))
                .subscribe(() =>
                  this._router.navigate(['/ref-tests'], {
                    state: { fromCreate: true },
                  }),
                );
            }
          }
        }),
        catchError((err) => {
          this.loading.set(false);
          this._bannerService.error(
            err.message || this._translate.instant('ref_tests.create.form.submit_error'),
          );
          return of(null);
        }),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }

  protected cancel(): void {
    this._router.navigate(['/']);
  }
}
