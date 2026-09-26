import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CreateParticipantGQL, UpdateParticipantGQL } from '../../../../graphql/generated';
import { Banner } from '../../services/banner';
import { runMutation } from '../../shared/utils/apollo-utils';
import {
  createEmptyParticipant,
  IParticipantFormValue,
} from '../models/participant-form-value';
import { ParticipantListItem, ParticipantsData } from '../services/participants-data';
import { RefTestUserListItem } from '../../ref-tests/create/components/ref-test-user-list-item/ref-test-user-list-item';

@Component({
  selector: 'app-manage-participants',
  imports: [TranslatePipe, RefTestUserListItem],
  templateUrl: './manage-participants.html',
  host: {
    class: 'block',
  },
})
export class ManageParticipants {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _createParticipantGQL = inject(CreateParticipantGQL);
  private readonly _updateParticipantGQL = inject(UpdateParticipantGQL);
  private readonly _translate = inject(TranslateService);
  private readonly _bannerService = inject(Banner);

  protected readonly participantsData = inject(ParticipantsData);
  protected readonly searchTerm = signal('');
  protected readonly participant = signal<IParticipantFormValue>(createEmptyParticipant());
  protected readonly selectedParticipantId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly participants = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    return this.participantsData.participants().filter((participant) => {
      if (!term) return true;
      return (
        participant.name.toLowerCase().includes(term) ||
        participant.email.toLowerCase().includes(term)
      );
    });
  });

  protected readonly isEditing = computed(() => this.selectedParticipantId() !== null);

  protected beginCreate(): void {
    this.selectedParticipantId.set(null);
    this.participant.set(createEmptyParticipant());
  }

  protected selectParticipant(participant: ParticipantListItem): void {
    this.selectedParticipantId.set(participant.id);
    this.participant.set({
      firstName: participant.firstName,
      lastName: participant.lastName,
      email: participant.email,
      type: participant.type,
      level: participant.level,
    });
  }

  protected onParticipantChange(participant: IParticipantFormValue): void {
    this.participant.set(participant);
  }

  protected save(): void {
    const participant = this.participant();
    const validationError = this.validateParticipant(participant);
    if (validationError) {
      this._bannerService.error(this._translate.instant(validationError));
      return;
    }

    this.saving.set(true);

    if (this.isEditing()) {
      runMutation(
        this._updateParticipantGQL.mutate({
          variables: {
            input: {
              id: this.selectedParticipantId() ?? '',
              participant,
            },
          },
        }),
        this._destroyRef,
        {
          onSuccess: async (payload) => {
            const result = payload.data?.updateParticipant;
            const message = result?.errors?.[0]?.message;
            if (message) {
              this._bannerService.error(message);
              return;
            }

            const updated = result?.participant;
            if (!updated) return;

            await this.participantsData.refresh();
            this.selectParticipant(updated);
            this._bannerService.success(
              this._translate.instant('participants.manage.messages.updated'),
            );
          },
          onError: () => {
            this._bannerService.error(
              this._translate.instant('participants.manage.messages.update_failed'),
            );
          },
          onComplete: () => this.saving.set(false),
        },
      );

      return;
    }

    runMutation(
      this._createParticipantGQL.mutate({
        variables: {
          input: {
            participant,
          },
        },
      }),
      this._destroyRef,
      {
        onSuccess: async (payload) => {
          const created = payload.data?.createParticipant.participant;
          if (!created) return;

          await this.participantsData.refresh();
          this.selectParticipant(created);
          this._bannerService.success(
            this._translate.instant('participants.manage.messages.created'),
          );
        },
        onError: () => {
          this._bannerService.error(
            this._translate.instant('participants.manage.messages.create_failed'),
          );
        },
        onComplete: () => this.saving.set(false),
      },
    );
  }

  private validateParticipant(participant: IParticipantFormValue): string | null {
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

    return null;
  }
}
