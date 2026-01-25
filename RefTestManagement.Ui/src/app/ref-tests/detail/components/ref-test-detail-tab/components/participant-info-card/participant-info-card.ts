import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-participant-info-card',
  imports: [TranslatePipe],
  templateUrl: './participant-info-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ParticipantInfoCard {
  name = input.required<string>();
  email = input.required<string>();
}
