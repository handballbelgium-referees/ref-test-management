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
import { catchError, delay, map, of, tap } from 'rxjs';
import { CreateBulkQuizSessionsGQL } from '../../../../graphql/generated';

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
      required(user.firstName, { message: 'First name is required' });
      required(user.email, { message: 'Email is required' });
      email(user.email, { message: 'Please enter a valid email address' });
    });

    // Validate quiz configuration
    required(schemaPath.numberOfQuestions, { message: 'Number of questions is required' });
    min(schemaPath.numberOfQuestions, 1, { message: 'Must have at least 1 question' });

    required(schemaPath.maxTimeInMinutes, { message: 'Time limit is required' });
    min(schemaPath.maxTimeInMinutes, 1, { message: 'Must have at least 1 minute' });
  });

  // Additional state signals
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly successCount = signal(0);
  protected readonly failedCount = signal(0);
  protected readonly errors = signal<Array<{ email: string; message: string }>>([]);
  protected readonly showBulkImport = signal(false);
  protected readonly bulkText = signal('');

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

    if (users.length === 0) {
      users.push({ firstName: '', lastName: '', email: '' });
    }

    const current = this.sessionModel();
    this.sessionModel.set({
      ...current,
      users,
    });

    this.showBulkImport.set(false);
    this.bulkText.set('');
  }

  protected onSubmit(): void {
    // Check form validity
    if (this.sessionForm().invalid()) {
      // Mark all fields as touched to reveal validation errors
      this.sessionForm().markAsTouched();
      this.error.set('Please fix the validation errors before submitting');
      return;
    }

    const formData = this.sessionModel();

    if (formData.users.length === 0) {
      this.error.set('Please add at least one participant');
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
          this.error.set(err.message || 'An error occurred while creating sessions');
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
