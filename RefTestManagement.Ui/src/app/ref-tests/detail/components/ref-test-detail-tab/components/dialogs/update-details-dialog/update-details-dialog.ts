import {
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { email, form, FormField, required } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import {
  GetRefTestByIdQuery,
  UpdateRefTestDetailsInput,
} from '../../../../../../../../../graphql/generated';
import { IsolatedBannerManager } from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';

type RefTest = Extract<GetRefTestByIdQuery['refTest'], { __typename: 'RefTest' }>;

interface IParticipantData {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
}

@Component({
  selector: 'app-update-details-dialog',
  imports: [TranslatePipe, FormField, Banner],
  templateUrl: './update-details-dialog.html',
})
export class UpdateDetailsDialog {
  protected readonly participantModel = signal<IParticipantData>({
    id: '',
    firstName: '',
    lastName: '',
    email: '',
  });

  protected readonly participantForm = form(this.participantModel, (schema) => {
    required(schema.id);
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

  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  readonly initialData = input.required<RefTest | undefined>();
  readonly bannerManager = input.required<IsolatedBannerManager>();
  protected readonly confirm = output<UpdateRefTestDetailsInput>();
  protected readonly cancel = output<void>();

  protected readonly canSave = computed(() => {
    return this.participantForm().valid() && !this.loading();
  });

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });

    effect(() => {
      const refTest = this.initialData();
      if (!refTest) return;
      this.participantModel.set({
        id: refTest.id,
        firstName: refTest.firstName,
        lastName: refTest.lastName,
        email: refTest.email,
      });
    });
  }

  protected onConfirm(): void {
    if (this.participantForm().invalid()) {
      return;
    }

    const data = this.participantModel();

    this.confirm.emit({
      id: data.id,
      firstName: data.firstName,
      lastName: data.lastName,
      email: data.email,
    });
  }
}
