import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { GetRefTestByIdQuery, RefTestStatus } from '../../../../../../../../graphql/generated';
import { HasPermission } from '../../../../../../auth/directives/has-permission.directive';
import { Permissions } from '../../../../../../auth/models/permissions';
import { Language, LANGUAGE_NAMES } from '../../../../../../services/language-config';

type RefTestTitle = NonNullable<
  Extract<GetRefTestByIdQuery['refTest'], { __typename: 'RefTest' }>
>['title'];

@Component({
  selector: 'app-test-info-card',
  imports: [TranslatePipe, HasPermission],
  templateUrl: './test-info-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class TestInfoCard {
  readonly title = input<RefTestTitle>();
  readonly numberOfQuestions = input.required<number>();
  readonly maxTimeInMinutes = input<number>();
  readonly language = input<string | null>();
  readonly status = input.required<RefTestStatus>();
  protected readonly Permissions = Permissions;
  protected readonly edit = output<void>();
  protected readonly regenerateToken = output<void>();

  protected readonly languageDisplayName = computed(() => {
    const lang = this.language();
    if (!lang) return null;
    return LANGUAGE_NAMES[lang as Language] ?? lang;
  });
}
