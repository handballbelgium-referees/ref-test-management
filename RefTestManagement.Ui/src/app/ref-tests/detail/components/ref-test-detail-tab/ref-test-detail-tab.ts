import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslatePipe } from '@ngx-translate/core';
import { onlyCompleteData } from 'apollo-angular';
import { map } from 'rxjs';
import { GetScoreConfigurationGQL, RefTestStatus } from '../../../../../../graphql/generated';
import { LocalizedDate } from '../../../../shared/pipes/localized-date';
import { RefTestDetailDataService } from '../../services/ref-test-detail-data.service';

@Component({
  selector: 'app-ref-test-detail-tab',
  imports: [TranslatePipe, LocalizedDate],
  templateUrl: './ref-test-detail-tab.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetailTab {
  private readonly _dataService = inject(RefTestDetailDataService);
  protected readonly refTest = this._dataService.refTest;

  protected readonly RefTestStatus = RefTestStatus;

  private readonly _passingPercentage = toSignal(
    inject(GetScoreConfigurationGQL)
      .watch()
      .valueChanges.pipe(
        onlyCompleteData(),
        map((result) => result.data.scoreConfiguration.passingPercentage),
      ),
    { initialValue: 0 },
  );

  protected readonly hasScore = computed(() => {
    const test = this.refTest();
    return test && test.questionScore !== null && test.questionScore !== undefined;
  });

  protected readonly isPassed = computed(() => {
    const test = this.refTest();
    if (!test) return false;
    const percentage = test.percentage;
    return (
      percentage !== null && percentage !== undefined && percentage >= this._passingPercentage()
    );
  });
}
