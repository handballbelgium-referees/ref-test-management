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
import { applyEach, email, Field, form, min, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, delay, map, of, switchMap, tap } from 'rxjs';
import {
  CreateBulkQuizSessionsGQL,
  SearchQuestionsByNumberGQL,
} from '../../../../graphql/generated';
import { BulkQuestionImportModal } from './components/bulk-question-import-modal/bulk-question-import-modal';
import { BulkUserImportModal } from './components/bulk-user-import-modal/bulk-user-import-modal';
import { QuestionSearchAutocomplete } from './components/question-search-autocomplete/question-search-autocomplete';
import { SessionUserListItem } from './components/session-user-list-item/session-user-list-item';
import { TitleAutocomplete } from './components/title-autocomplete/title-autocomplete';

interface UserData {
  firstName: string;
  lastName: string;
  email: string;
}

interface SessionFormData {
  users: UserData[];
  title: { id?: string; name: string } | null;
  numberOfQuestions: number;
  maxTimeInMinutes: number;
  specificQuestionNumbers: string;
  sendInvitations: boolean;
}

@Component({
  selector: 'app-create-sessions',
  imports: [
    TranslatePipe,
    Field,
    SessionUserListItem,
    BulkUserImportModal,
    QuestionSearchAutocomplete,
    BulkQuestionImportModal,
    TitleAutocomplete,
  ],
  templateUrl: './create-sessions.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateSessions {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _createBulkQuizSessionsGQL = inject(CreateBulkQuizSessionsGQL);
  private readonly _searchQuestionsByNumberGQL = inject(SearchQuestionsByNumberGQL);
  private readonly _router = inject(Router);
  private readonly _translate = inject(TranslateService);

  protected readonly messagesContainer = viewChild<ElementRef>('messagesContainer');

  // Angular v21 Signal Forms - model signal
  protected readonly sessionModel = signal<SessionFormData>({
    users: [{ firstName: '', lastName: '', email: '' }],
    title: null,
    numberOfQuestions: 30,
    maxTimeInMinutes: 60,
    specificQuestionNumbers: '',
    sendInvitations: false,
  });

  // Form field tree with validation schema
  protected readonly sessionForm = form(this.sessionModel, (schemaPath) => {
    // Validate each user in the array
    applyEach(schemaPath.users, (user) => {
      required(user.firstName, {
        message: 'sessions.create.form.users.firstNameRequired',
      });
      required(user.lastName, {
        message: 'sessions.create.form.users.lastNameRequired',
      });
      required(user.email, {
        message: 'sessions.create.form.users.emailRequired',
      });
      email(user.email, {
        message: 'sessions.create.form.users.emailInvalid',
      });
    });

    // Validate title
    required(schemaPath.title, {
      message: 'sessions.create.form.title.required',
    });

    // Validate quiz configuration
    required(schemaPath.numberOfQuestions, {
      message: 'sessions.create.form.numberOfQuestions.required',
    });
    min(schemaPath.numberOfQuestions, 1, {
      message: 'sessions.create.form.numberOfQuestions.min',
    });

    required(schemaPath.maxTimeInMinutes, {
      message: 'sessions.create.form.maxTimeInMinutes.required',
    });
    min(schemaPath.maxTimeInMinutes, 1, {
      message: 'sessions.create.form.maxTimeInMinutes.min',
    });
  });

  // Additional state signals
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly successCount = signal(0);
  protected readonly failedCount = signal(0);
  protected readonly errors = signal<Array<{ email: string; message: string }>>([]);
  protected readonly showBulkImport = signal(false);
  protected readonly selectedQuestions = signal<
    Array<{ number: string; phrase: Record<string, string> }>
  >([]);
  protected readonly showBulkQuestionImport = signal(false);
  readonly currentLanguage = toSignal(
    this._translate.onLangChange.pipe(map(() => this._translate.getCurrentLang())),
    {
      initialValue: this._translate.getCurrentLang(),
    }
  );

  // Computed signals
  protected readonly userCount = computed(() => this.sessionModel().users.length);

  protected addUser(): void {
    const current = this.sessionModel();
    this.sessionModel.set({
      ...current,
      users: [...current.users, { firstName: '', lastName: '', email: '' }],
    });
  }

  protected removeUser(index: number): void {
    const current = this.sessionModel();
    const newUsers = current.users.filter((_, i) => i !== index);

    // Ensure at least one user remains
    if (newUsers.length === 0) {
      newUsers.push({ firstName: '', lastName: '', email: '' });
    }

    this.sessionModel.set({
      ...current,
      users: newUsers,
    });
  }

  protected toggleBulkImport(): void {
    this.showBulkImport.update((v) => !v);
  }

  protected toggleBulkQuestionImport(): void {
    this.showBulkQuestionImport.update((v) => !v);
  }

  protected onTitleSelect(title: { id?: string; name: string }): void {
    const current = this.sessionModel();
    this.sessionModel.set({
      ...current,
      title: title.name ? title : null,
    });
  }

  protected onQuestionSelect(question: { number: string; phrase: Record<string, string> }): void {
    const current = this.selectedQuestions();
    if (!current.some((q) => q.number === question.number)) {
      this.selectedQuestions.set([...current, question]);
      this.updateQuestionNumbersField();
    }
  }

  protected onQuestionRemove(questionNumber: string): void {
    this.selectedQuestions.update((current) => current.filter((q) => q.number !== questionNumber));
    this.updateQuestionNumbersField();
  }

  private updateQuestionNumbersField(): void {
    const numbers = this.selectedQuestions()
      .map((q) => q.number)
      .join(', ');
    const current = this.sessionModel();
    this.sessionModel.set({
      ...current,
      specificQuestionNumbers: numbers,
    });
  }

  protected onBulkQuestionImport(text: string): void {
    const lines = text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    const questionNumbers = lines
      .flatMap((line) => line.split(','))
      .map((num) => num.trim())
      .filter((num) => num.length > 0);

    if (questionNumbers.length === 0) {
      this.showBulkQuestionImport.set(false);
      return;
    }

    // Validate and add questions
    const validationRequests = questionNumbers.map((number) =>
      this._searchQuestionsByNumberGQL.fetch({ variables: { number } }).pipe(
        map((result) => {
          const question = result.data?.searchQuestionsByNumber?.[0];
          return question
            ? { number: question.number, phrase: question.phrase, isValid: true }
            : null;
        }),
        catchError(() => of(null))
      )
    );

    of(validationRequests)
      .pipe(
        switchMap((requests) => Promise.all(requests.map((r) => r.toPromise()))),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe((results) => {
        const validQuestions = results
          .filter(
            (r): r is { number: string; phrase: Record<string, string>; isValid: boolean } =>
              r !== null && (r?.isValid ?? false)
          )
          .filter((q) => !this.selectedQuestions().some((sq) => sq.number === q.number));

        if (validQuestions.length > 0) {
          this.selectedQuestions.update((current) => [...current, ...validQuestions]);
          this.updateQuestionNumbersField();
        }

        this.showBulkQuestionImport.set(false);
      });
  }

  protected onBulkUserImport(text: string): void {
    const lines = text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    const users: UserData[] = [];

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
      const current = this.sessionModel();
      // Filter out empty users (all fields empty)
      const nonEmptyUsers = current.users.filter(
        (user) => user.firstName.trim() || user.lastName.trim() || user.email.trim()
      );
      this.sessionModel.set({
        ...current,
        users: [...nonEmptyUsers, ...users],
      });
    }

    this.showBulkImport.set(false);
  }

  protected onBulkUserCancel(): void {
    this.showBulkImport.set(false);
  }

  protected onBulkQuestionCancel(): void {
    this.showBulkQuestionImport.set(false);
  }

  protected onSubmit(): void {
    // Check form validity
    if (this.sessionForm().invalid()) {
      // Mark all fields as touched to reveal validation errors
      this.sessionForm().markAsTouched();
      this.error.set('sessions.create.form.validationError');
      return;
    }

    const formData = this.sessionModel();

    if (formData.users.length === 0) {
      this.error.set('sessions.create.form.noParticipants');
      return;
    }

    const specificQuestions = formData.specificQuestionNumbers
      .split(',')
      .map((q) => q.trim())
      .filter((q) => q.length > 0);

    this.error.set(null);
    this.successCount.set(0);
    this.failedCount.set(0);
    this.errors.set([]);

    this._createBulkQuizSessionsGQL
      .mutate({
        variables: {
          input: {
            users: formData.users,
            title: {
              id: formData.title?.id,
              name: formData.title?.name,
            },
            numberOfQuestions: formData.numberOfQuestions,
            maxTimeInMinutes: formData.maxTimeInMinutes,
            specificQuestionNumbers: specificQuestions.length > 0 ? specificQuestions : undefined,
            sendInvitations: formData.sendInvitations,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        map((result) => result.data?.createBulkQuizSessions?.bulkQuizSessionResult),
        tap((data) => {
          if (data) {
            this.successCount.set(data.successfullyCreated);
            this.failedCount.set(data.failed);

            if (data.errors.length > 0) {
              this.errors.set(
                data.errors.map((e) => ({
                  email: e.user.email,
                  message: e.errorMessage,
                }))
              );
            }

            if (data.successfullyCreated > 0) {
              // Reset form to initial state
              this.sessionModel.set({
                users: [{ firstName: '', lastName: '', email: '' }],
                title: null,
                numberOfQuestions: 30,
                maxTimeInMinutes: 60,
                specificQuestionNumbers: '',
                sendInvitations: false,
              });
              // Reset form state
              this.sessionForm().reset();
            }

            // Scroll to messages
            setTimeout(() => {
              this.messagesContainer()?.nativeElement.scrollIntoView({
                behavior: 'smooth',
                block: 'center',
              });
            }, 100);

            if (data.successfullyCreated > 0 && data.failed === 0) {
              of(null)
                .pipe(delay(2000), takeUntilDestroyed(this._destroyRef))
                .subscribe(() => this._router.navigate(['/sessions']));
            }
          }
        }),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(err.message || 'sessions.create.form.submitError');
          // Scroll to error message
          setTimeout(() => {
            this.messagesContainer()?.nativeElement.scrollIntoView({
              behavior: 'smooth',
              block: 'center',
            });
          }, 100);
          return of(null);
        }),
        takeUntilDestroyed(this._destroyRef)
      )
      .subscribe();
  }

  protected cancel(): void {
    this._router.navigate(['/']);
  }
}
