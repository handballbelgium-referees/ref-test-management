import { Component, input } from '@angular/core';
import { DeepPartial } from '@apollo/client/utilities';
import { TranslatePipe } from '@ngx-translate/core';
import { GetRefTestByTokenQuery } from '../../../../../../graphql/generated';

type RefTest = DeepPartial<
  Extract<GetRefTestByTokenQuery['refTestByToken'], { __typename?: 'ParticipantRefTest' }>
>;

@Component({
  selector: 'app-ref-test-details',
  imports: [TranslatePipe],
  templateUrl: './ref-test-details.html',
  host: {
    class: 'block',
  },
})
export class RefTestDetails {
  readonly refTest = input.required<RefTest>();
}
