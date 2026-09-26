import { Component, computed, DestroyRef, inject, signal, viewChild, ElementRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { disabled, form, FormField, min, required } from '@angular/forms/signals';
import { Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, map, of, tap } from 'rxjs';
import {
  CreateRefTestsGQL,
  CreateRefTestsMutation,
  GetQuestionsByNumberGQL,
} from '../../../../graphql/generated';
import { Banner } from '../../services/banner';
import { DatetimePicker } from '../../shared/components/datetime-picker/datetime-picker';
import { runMutation } from '../../shared/utils/apollo-utils';
import {
  createEmptyParticipant,
  IParticipantFormValue,
  isParticipantDraftEmpty,
} from '../../participants/models/participant-form-value';
import { ParticipantsData } from '../../participants/services/participants-data';
import { QuestionImportModal } from './components/question-import-modal/question-import-modal';
import { QuestionSearchAutocomplete } from './components/question-search-autocomplete/question-search-autocomplete';
import { RefTestUserListItem } from './components/ref-test-user-list-item/ref-test-user-list-item';
import { TitleAutocomplete } from './components/title-autocomplete/title-autocomplete';
import { UserImportModal } from './components/user-import-modal/user-import-modal';

interface IRefTestFormData {
  title: { id?: string; name: string } | null;
  numberOfQuestions: number;
  randomQuestionsForEachUser: boolean;
  maxTimeInMinutes: number;
  specificQuestionNumbers: string;
  sendInvitations: boolean;
  sendResults: boolean;
  scheduledAt: string;
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
    DatetimePicker,
  ],
  templateUrl: './create-ref-tests.html',
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

  protected readonly participantsData = inject(ParticipantsData);
  protected readonly messagesContainer = viewChild<ElementRef>('messagesContainer');

  protected readonly selectedParticipantIds = signal<string[]>([]);
  protected readonly newParticipants = signal<IParticipantFormValue[]>([]);
  protected readonly participantSearch = signal('');

  protected readonly refTestModel = signal<IRefTestFormData>({
    title: null,
    numberOfQuestions: 30,
    randomQuestionsForEachUser: false,
    maxTimeInMinutes: 60,
    specificQuestionNumbers: '',
    sendInvitations: false,
    sendResults: false,
    scheduledAt: '',
  });

  protected readonly refTestForm = form(this.refTestModel, (schemaPath) => {
    required(schemaPath.title, {
      message: 'ref_tests.create.form.title.required',
    });

    required(schemaPath.numberOfQuestions, {
      message: 'ref_tests.create.form.number_of_questions.required',
      when: () => this.selectedQuestions().length === 0,
    });
    disabled(schemaPath.numberOfQuestions, () => this.selectedQuestions().length > 0);
    disabled(schemaPath.randomQuestionsForEachUser, () => this.selectedQuestions().length > 0);
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

  protected readonly loading = signal(false);
  protected readonly showImport = signal(false);
  protected readonly selectedQuestions = signal<Array<{ number: string; phrase: Record<string, string> }>>([]);
  protected readonly showQuestionImport = signal(false);
  protected readonly loadingQuestions = signal(false);
  protected readonly currentLanguage = this._translate.currentLang;

  protected readonly selectedParticipants = computed(() => {
    const selectedIds = new Set(this.selectedParticipantIds());
    return this.participantsData.participants().filter((participant) => selectedIds.has(participant.id));
  });

  protected readonly availableParticipants = computed(() => {
    const term = this.participantSearch().trim().toLowerCase();
    return this.participantsData.participants().filter((participant) => {
      if (!term) return true;
      return (
        participant.name.toLowerCase().includes(term) ||
        participant.email.toLowerCase().includes(term)
      );
    });
  });

  protected readonly userCount = computed(() => {
    return this.selectedParticipantIds().length + this.nonEmptyParticipants().length;
  });

  protected addParticipant(): void {
    this.newParticipants.update((participants) => [...participants, createEmptyParticipant()]);
  }

  protected updateParticipant(index: number, participant: IParticipantFormValue): void {
    this.newParticipants.update((participants) =>
      participants.map((current, currentIndex) => (currentIndex === index ? participant : current)),
    );
  }

  protected removeParticipant(index: number): void {
    this.newParticipants.update((participants) => participants.filter((_, currentIndex) => currentIndex !== index));
  }

  protected toggleExistingParticipant(id: string): void {
    this.selectedParticipantIds.update((ids) =>
      ids.includes(id) ? ids.filter((current) => current !== id) : [...ids, id],
    );
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

    this._getQuestionsByNumberGQL
      .fetch({ variables: { numbers: questionNumbers } })
      .pipe(
        map((result) => {
          const questions = result.data?.questionsByNumber ?? [];
          return questions
            .filter((q) => q.phrase != null)
            .map((q) => ({
              number: q.number,
              phrase: q.phrase as Record<string, string>,
            }))
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

    const participants: IParticipantFormValue[] = [];

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
        participants.push({
          firstName,
          lastName,
          email,
          type: 'OTHER',
          level: null,
        });
      }
    });

    if (participants.length > 0) {
      this.newParticipants.update((current) => [
        ...current.filter((participant) => !isParticipantDraftEmpty(participant)),
        ...participants,
      ]);
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
    if (this.refTestForm().invalid()) {
      this.refTestForm().markAsTouched();
      this._bannerService.error(this._translate.instant('ref_tests.create.form.validation_error'));
      return;
    }

    const draftParticipants = this.nonEmptyParticipants();
    if (this.selectedParticipantIds().length === 0 && draftParticipants.length === 0) {
      this._bannerService.error(this._translate.instant('ref_tests.create.form.no_participants'));
      return;
    }

    const participantValidationError = this.validateParticipants(draftParticipants);
    if (participantValidationError) {
      this._bannerService.error(this._translate.instant(participantValidationError));
      return;
    }

    const formData = this.refTestModel();
    const specificQuestions = formData.specificQuestionNumbers
      .split(',')
      .map((q) => q.trim())
      .filter((q) => q.length > 0);

    const { loading } = runMutation(
      this._createRefTestsGQL.mutate({
        variables: {
          input: {
            participantIds: this.selectedParticipantIds(),
            newParticipants: draftParticipants,
            title: formData.title?.id ? { id: formData.title.id } : { name: formData.title?.name ?? '' },
            numberOfQuestions: formData.numberOfQuestions,
            randomQuestionsForEachUser: formData.randomQuestionsForEachUser,
            maxTimeInMinutes: formData.maxTimeInMinutes,
            specificQuestionNumbers: specificQuestions.length > 0 ? specificQuestions : undefined,
            sendAutomatedInvitations: formData.sendInvitations,
            sendAutomatedResults: formData.sendResults,
            scheduledAt:
              formData.sendInvitations && formData.scheduledAt
                ? new Date(formData.scheduledAt).toISOString()
                : undefined,
          },
        },
      }),
      this._destroyRef,
      {
        onSuccess: (data: CreateRefTestsMutation['createRefTests']['createRefTestsResult']) => {
          if (!data) return;

          if (data.errors.length > 0) {
            this._bannerService.error(
              this._translate.instant('ref_tests.create.success.failed_info', {
                count: data.failed,
              }),
            );
          }

          if (data.successfullyCreated > 0) {
            const successKey =
              data.successfullyCreated === 1
                ? 'ref_tests.create.success.created_one'
                : 'ref_tests.create.success.created_other';
            this._bannerService.success(
              this._translate.instant(successKey, { count: data.successfullyCreated }),
            );

            this.selectedParticipantIds.set([]);
            this.newParticipants.set([]);
            this.participantSearch.set('');
            this.refTestModel.set({
              title: null,
              numberOfQuestions: 30,
              randomQuestionsForEachUser: false,
              maxTimeInMinutes: 60,
              specificQuestionNumbers: '',
              sendInvitations: false,
              sendResults: false,
              scheduledAt: '',
            });
            this.refTestForm().reset();
          }

          if (data.successfullyCreated > 0 && data.failed === 0) {
            this._router.navigate(['/ref-tests'], {
              state: { fromCreate: true },
            });
          }
        },
        onError: (err) => {
          const message = err instanceof Error ? err.message : null;
          this._bannerService.error(message || this._translate.instant('ref_tests.create.form.submit_error'));
        },
      },
      (result) => result.data?.createRefTests?.createRefTestsResult ?? null,
    );
    this.loading.set(loading());
  }

  protected cancel(): void {
    this._router.navigate(['/']);
  }

  protected onScheduledAtChange(value: string): void {
    this.refTestModel.update((m) => ({ ...m, scheduledAt: value }));
  }

  protected nonEmptyParticipants(): IParticipantFormValue[] {
    return this.newParticipants().filter((participant) => !isParticipantDraftEmpty(participant));
  }

  private validateParticipants(participants: IParticipantFormValue[]): string | null {
    for (const participant of participants) {
      if (!participant.firstName.trim()) return 'ref_tests.create.form.users.first_name_required';
      if (!participant.lastName.trim()) return 'ref_tests.create.form.users.last_name_required';
      if (!participant.email.trim()) return 'ref_tests.create.form.users.email_required';
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(participant.email.trim())) {
        return 'ref_tests.create.form.users.email_invalid';
      }
      if (participant.type === 'REFEREE' && !participant.level) {
        return 'ref_tests.create.form.users.level_required';
      }
      if (participant.type !== 'REFEREE' && participant.level) {
        return 'participants.manage.messages.level_only_for_referees';
      }
    }

    return null;
  }
}
