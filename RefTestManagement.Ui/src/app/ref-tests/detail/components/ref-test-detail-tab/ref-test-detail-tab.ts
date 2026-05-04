import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { onlyCompleteData } from 'apollo-angular';
import { map } from 'rxjs';
import {
  ExtendRefTestTimeInput,
  GetScoreConfigurationGQL,
  UpdateRefTestConfigurationInput,
  UpdateRefTestDetailsInput,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../../graphql/generated';
import { IResetOptions } from '../../../list/services/types';
import { RefTestDetailData } from '../../services/ref-test-detail-data';
import { RefTestDetailOperationManager } from '../../services/ref-test-detail-operation-manager';
import { DetailsCard } from './components/details-card/details-card';
import { ExtendTimeDialog } from './components/dialogs/extend-time-dialog/extend-time-dialog';
import { RegenerateTokenDialog } from './components/dialogs/regenerate-token-dialog/regenerate-token-dialog';
import { ResetRefTestDialog } from './components/dialogs/reset-ref-test-dialog/reset-ref-test-dialog';
import { ReviveRefTestDialog } from './components/dialogs/revive-ref-test-dialog/revive-ref-test-dialog';
import { UpdateConfigurationDialog } from './components/dialogs/update-configuration-dialog/update-configuration-dialog';
import { UpdateDetailsDialog } from './components/dialogs/update-details-dialog/update-details-dialog';
import { UpdateNotificationSettingsDialog } from './components/dialogs/update-notification-settings-dialog/update-notification-settings-dialog';
import { ScoresCard } from './components/scores-card/scores-card';
import { StatusInfoCard } from './components/status-info-card/status-info-card';
import { TestInfoCard } from './components/test-info-card/test-info-card';
import { TimelineCard } from './components/timeline-card/timeline-card';

@Component({
  selector: 'app-ref-test-detail-tab',
  imports: [
    DetailsCard,
    TestInfoCard,
    StatusInfoCard,
    TimelineCard,
    ScoresCard,
    UpdateDetailsDialog,
    UpdateConfigurationDialog,
    UpdateNotificationSettingsDialog,
    ExtendTimeDialog,
    RegenerateTokenDialog,
    ResetRefTestDialog,
    ReviveRefTestDialog,
  ],
  providers: [RefTestDetailOperationManager],
  templateUrl: './ref-test-detail-tab.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetailTab {
  private readonly _dataService = inject(RefTestDetailData);
  protected readonly operationManager = inject(RefTestDetailOperationManager);

  protected readonly refTest = this._dataService.refTestData;

  protected readonly regenerateTokenDialog = viewChild(RegenerateTokenDialog);
  protected readonly showRegenerateTokenDialog = signal(false);

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
    const refTest = this.refTest();
    return refTest && refTest.questionScore !== null && refTest.questionScore !== undefined;
  });

  protected readonly isPassed = computed(() => {
    const refTest = this.refTest();
    if (!refTest) return false;
    const percentage = refTest.percentage;
    return (
      percentage !== null && percentage !== undefined && percentage >= this._passingPercentage()
    );
  });

  protected confirmUpdateDetails(input: UpdateRefTestDetailsInput): void {
    this.operationManager.updateRefTestDetailsDialog.confirm(input);
  }

  protected confirmUpdateConfiguration(input: UpdateRefTestConfigurationInput): void {
    this.operationManager.updateRefTestConfigurationDialog.confirm(input);
  }

  protected confirmUpdateNotificationSettings(input: UpdateRefTestNotificationSettingsInput): void {
    this.operationManager.updateRefTestNotificationSettingsDialog.confirm(input);
  }

  protected extendTime(input: ExtendRefTestTimeInput): void {
    this.operationManager.extendRefTestTimeDialog.confirm(input);
  }

  protected confirmRegenerateToken(): void {
    this.operationManager.regenerateRefTestTokenDialog.confirm({
      refTestId: this.refTest().id,
    });
  }

  protected confirmReset(options: IResetOptions): void {
    this.operationManager.resetDialog.confirm(options);
  }

  protected confirmRevive(): void {
    this.operationManager.reviveDialog.confirm();
  }
}
