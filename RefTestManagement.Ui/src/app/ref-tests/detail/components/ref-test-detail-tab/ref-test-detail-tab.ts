import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { RefTestStatus } from '../../../../../../graphql/generated';
import { LocalizedDate } from '../../../../shared/pipes/localized-date';
import { RefTestDetailDataService } from '../../services/ref-test-detail-data.service';

@Component({
  selector: 'app-ref-test-detail-tab',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './ref-test-detail-tab.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetailTab {
  private readonly dataService = inject(RefTestDetailDataService);
  protected readonly refTest = this.dataService.refTest;

  protected readonly RefTestStatus = RefTestStatus;

  protected readonly hasScore = computed(() => {
    const test = this.refTest();
    return test && test.questionScore !== null && test.questionScore !== undefined;
  });

  protected readonly isPassed = computed(() => {
    const test = this.refTest();
    if (!test) return false;
    const percentage = test.percentage;
    return percentage !== null && percentage !== undefined && percentage >= 80;
  });
}
