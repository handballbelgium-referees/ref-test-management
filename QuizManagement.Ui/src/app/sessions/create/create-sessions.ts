import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { applyEach, email, Field, form, min, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  catchError,
  debounceTime,
  delay,
  distinctUntilChanged,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import {
  CreateBulkQuizSessionsGQL,
  SearchQuestionsByNumberGQL,
} from '../../../../graphql/generated';

interface UserData {
  firstName: string;
  lastName: string;
  email: string;
}

interface SessionFormData {
  users: UserData[];
  numberOfQuestions: number;
  maxTimeInMinutes: number;
  specificQuestionNumbers: string;
  sendInvitations: boolean;
}

@Component({
  selector: 'app-create-sessions',
  imports: [TranslatePipe, Field],
  templateUrl: './create-sessions.html',
  styleUrl: './create-sessions.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateSessions {
  private readonly destroyRef = inject(DestroyRef);
  private readonly createBulkQuizSessionsGQL = inject(CreateBulkQuizSessionsGQL);
  private readonly searchQuestionsByNumberGQL = inject(SearchQuestionsByNumberGQL);
  private readonly router = inject(Router);

  // Angular v21 Signal Forms - model signal
  protected readonly sessionModel = signal<SessionFormData>({
    users: [{ firstName: '', lastName: '', email: '' }],
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
  protected readonly bulkText = signal('');

  // Autocomplete state
  protected readonly questionSearchTerm = signal('');
  protected readonly searchingQuestions = signal(false);
  protected readonly questionSuggestions = signal<
    Array<{ number: string; phrase: string; id: string }>
  >([]);
  protected readonly showDropdown = signal(false);
  protected readonly highlightedIndex = signal(-1);
  protected readonly selectedQuestions = signal<Array<{ number: string; phrase: string }>>([]);
  protected readonly showBulkQuestionImport = signal(false);
  protected readonly bulkQuestionText = signal('');

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

  protected onQuestionSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value;
    this.questionSearchTerm.set(value);

    if (!value || value.trim().length < 1) {
      this.showDropdown.set(false);
      this.questionSuggestions.set([]);
      return;
    }

    this.searchingQuestions.set(true);
    this.showDropdown.set(true);

    of(value)
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((searchTerm) =>
          this.searchQuestionsByNumberGQL.fetch({ variables: { number: searchTerm } }).pipe(
            map((result) =>
              (result.data?.searchQuestionsByNumber || []).map((q) => ({
                number: q.number,
                phrase: q.phrase,
                id: q.id,
              }))
            ),
            catchError(() => of([]))
          )
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((results) => {
        this.searchingQuestions.set(false);
        this.questionSuggestions.set(results);

        // Auto-highlight if only one suggestion
        if (results.length === 1) {
          this.highlightedIndex.set(0);
        }
      });
  }

  protected selectQuestion(question: { number: string; phrase: string }): void {
    const current = this.selectedQuestions();
    if (!current.some((q) => q.number === question.number)) {
      this.selectedQuestions.set([...current, question]);
      this.updateQuestionNumbersField();
    }
    this.questionSearchTerm.set('');
    this.showDropdown.set(false);
    this.highlightedIndex.set(-1);
  }

  protected onQuestionKeydown(event: KeyboardEvent): void {
    const suggestions = this.questionSuggestions();
    if (suggestions.length === 0 || !this.showDropdown()) {
      return;
    }

    const currentIndex = this.highlightedIndex();

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        const nextIndex = currentIndex < suggestions.length - 1 ? currentIndex + 1 : 0;
        this.highlightedIndex.set(nextIndex);
        break;

      case 'ArrowUp':
        event.preventDefault();
        const prevIndex = currentIndex > 0 ? currentIndex - 1 : suggestions.length - 1;
        this.highlightedIndex.set(prevIndex);
        break;

      case 'Enter':
        event.preventDefault();
        if (currentIndex >= 0 && currentIndex < suggestions.length) {
          this.selectQuestion(suggestions[currentIndex]);
        }
        break;

      case 'Escape':
        event.preventDefault();
        this.showDropdown.set(false);
        this.highlightedIndex.set(-1);
        break;
    }
  }

  protected removeSelectedQuestion(questionNumber: string): void {
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

  protected closeDropdown(): void {
    setTimeout(() => {
      this.showDropdown.set(false);
      this.highlightedIndex.set(-1);
    }, 200);
  }

  protected importBulkQuestions(): void {
    const text = this.bulkQuestionText();
    const lines = text
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.length > 0);

    const questionNumbers = lines
      .flatMap((line) => line.split(','))
      .map((num) => num.trim())
      .filter((num) => num.length > 0);

    if (questionNumbers.length === 0) {
      return;
    }

    // Validate and add questions
    this.searchingQuestions.set(true);
    const validationRequests = questionNumbers.map((number) =>
      this.searchQuestionsByNumberGQL.fetch({ variables: { number } }).pipe(
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
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((results) => {
        this.searchingQuestions.set(false);
        const validQuestions = results
          .filter(
            (r): r is { number: string; phrase: string; isValid: boolean } =>
              r !== null && (r?.isValid ?? false)
          )
          .filter((q) => !this.selectedQuestions().some((sq) => sq.number === q.number));

        if (validQuestions.length > 0) {
          this.selectedQuestions.update((current) => [...current, ...validQuestions]);
          this.updateQuestionNumbersField();
        }

        this.showBulkQuestionImport.set(false);
        this.bulkQuestionText.set('');
      });
  }

  protected importBulk(): void {
    const text = this.bulkText();
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
    this.bulkText.set('');
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

    this.loading.set(true);
    this.error.set(null);
    this.successCount.set(0);
    this.failedCount.set(0);
    this.errors.set([]);

    this.createBulkQuizSessionsGQL
      .mutate({
        variables: {
          input: {
            users: formData.users,
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
                numberOfQuestions: 30,
                maxTimeInMinutes: 60,
                specificQuestionNumbers: '',
                sendInvitations: false,
              });
              // Reset form state
              this.sessionForm().reset();
            }

            if (data.successfullyCreated > 0 && data.failed === 0) {
              of(null)
                .pipe(delay(2000), takeUntilDestroyed(this.destroyRef))
                .subscribe(() => this.router.navigate(['/sessions']));
            }
          }
        }),
        catchError((err) => {
          this.loading.set(false);
          this.error.set(err.message || 'sessions.create.form.submitError');
          return of(null);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe();
  }

  protected cancel(): void {
    this.router.navigate(['/']);
  }
}
