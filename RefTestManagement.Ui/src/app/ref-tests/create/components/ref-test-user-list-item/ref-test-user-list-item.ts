import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import {
  IParticipantFormValue,
  PARTICIPANT_LEVEL_OPTIONS,
  PARTICIPANT_TYPE_OPTIONS,
} from '../../../../participants/models/participant-form-value';

@Component({
  selector: 'app-ref-test-user-list-item',
  imports: [TranslatePipe],
  templateUrl: './ref-test-user-list-item.html',
  host: {
    class: 'block',
  },
})
export class RefTestUserListItem {
  readonly participant = input.required<IParticipantFormValue>();
  readonly index = input.required<number>();
  readonly showRemove = input<boolean>(true);

  protected readonly participantChange = output<IParticipantFormValue>();
  protected readonly remove = output<void>();
  protected readonly typeOptions = PARTICIPANT_TYPE_OPTIONS;
  protected readonly levelOptions = PARTICIPANT_LEVEL_OPTIONS;

  protected onFieldChange<K extends keyof IParticipantFormValue>(
    field: K,
    value: IParticipantFormValue[K],
  ): void {
    const next = { ...this.participant(), [field]: value } as IParticipantFormValue;
    if (field === 'type' && value !== 'REFEREE') {
      next.level = null;
    }

    this.participantChange.emit(next);
  }
}
