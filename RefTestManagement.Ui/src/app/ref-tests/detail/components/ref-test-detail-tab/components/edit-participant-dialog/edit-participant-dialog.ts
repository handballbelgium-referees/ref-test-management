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
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { email, form, FormField, required } from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { catchError, EMPTY, finalize, tap } from 'rxjs';
import { UpdateRefTestDetailsGQL } from '../../../../../../../../graphql/generated';
import { Toast } from '../../../../../../services/toast';

interface IParticipantData {
  firstName: string;
  lastName: string;
  email: string;
}

@Component({
  selector: 'app-edit-participant-dialog',
  imports: [TranslatePipe, FormField],
  templateUrl: './edit-participant-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditParticipantDialog {
  private readonly _updateRefTestDetailsGQL = inject(UpdateRefTestDetailsGQL);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _toast = inject(Toast);
  private readonly _translateService = inject(TranslateService);

  protected readonly participantModel = signal<IParticipantData>({
    firstName: '',
    lastName: '',
    email: '',
  });

  protected readonly participantForm = form(this.participantModel, (schema) => {
    required(schema.firstName, {
      message: 'ref_tests.detail.edit_participant.first_name_required',
    });
    required(schema.lastName, {
      message: 'ref_tests.detail.edit_participant.last_name_required',
    });
    required(schema.email, {
      message: 'ref_tests.detail.edit_participant.email_required',
    });
    email(schema.email, {
      message: 'ref_tests.detail.edit_participant.email_invalid',
    });
  });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  readonly show = input.required<boolean>();
  readonly refTestId = signal<string>('');
  readonly closeDialog = output<void>();
  readonly cancel = output<void>();

  protected readonly canSave = computed(() => {
    return this.participantForm().valid() && !this.loading();
  });

  initialize(refTestId: string, firstName: string, lastName: string, email: string): void {
    this.refTestId.set(refTestId);
    this.participantModel.set({ firstName, lastName, email });
  }

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });
  }

  protected onSave(): void {
    if (this.participantForm().invalid()) {
      this.participantForm().markAsTouched();
      return;
    }

    const data = this.participantModel();
    this.error.set(null);

    this._updateRefTestDetailsGQL
      .mutate({
        variables: {
          input: {
            id: this.refTestId(),
            firstName: data.firstName,
            lastName: data.lastName,
            email: data.email,
          },
        },
      })
      .pipe(
        tap((result) => this.loading.set(result.loading ?? false)),
        tap((result) => {
          if (result.data?.updateRefTestDetails.errors?.length) {
            const error = result.data.updateRefTestDetails.errors[0];
            this.error.set(this._translateService.instant(error.message));
          } else if (result.data?.updateRefTestDetails.refTest) {
            this._toast.success(
              this._translateService.instant('ref_tests.detail.edit_participant.success'),
            );
            this.closeDialog.emit();
          }
        }),
        catchError(() => {
          this.error.set(this._translateService.instant('ref_tests.detail.edit_participant.error'));
          return EMPTY;
        }),
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this._destroyRef),
      )
      .subscribe();
  }
}
