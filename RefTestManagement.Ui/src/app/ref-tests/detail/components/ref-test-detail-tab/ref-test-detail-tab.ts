import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
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
  RefTestStatus,
  RegenerateRefTestTokenInput,
  UpdateRefTestConfigurationInput,
  UpdateRefTestDetailsInput,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../../graphql/generated';
import { IsolatedBannerManager } from '../../../../services/banner';
import { IResetOptions } from '../../../list/services/types';
import { RefTestDetailData } from '../../services/ref-test-detail-data';
import { RefTestDetailOperationManager } from '../../services/ref-test-detail-operation-manager';
import { ExtendTimeDialog } from './components/dialogs/extend-time-dialog/extend-time-dialog';
import { RegenerateTokenDialog } from './components/dialogs/regenerate-token-dialog/regenerate-token-dialog';
import { ResetRefTestDialog } from './components/dialogs/reset-ref-test-dialog/reset-ref-test-dialog';
import { ReviveRefTestDialog } from './components/dialogs/revive-ref-test-dialog/revive-ref-test-dialog';
import { UpdateConfigurationDialog } from './components/dialogs/update-configuration-dialog/update-configuration-dialog';
import { UpdateDetailsDialog } from './components/dialogs/update-details-dialog/update-details-dialog';
import { UpdateNotificationSettingsDialog } from './components/dialogs/update-notification-settings-dialog/update-notification-settings-dialog';
import { ParticipantInfoCard } from './components/participant-info-card/participant-info-card';
import { ScoresCard } from './components/scores-card/scores-card';
import { StatusInfoCard } from './components/status-info-card/status-info-card';
import { TestInfoCard } from './components/test-info-card/test-info-card';
import { TimelineCard } from './components/timeline-card/timeline-card';

@Component({
  selector: 'app-ref-test-detail-tab',
  imports: [
    ParticipantInfoCard,
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
  templateUrl: './ref-test-detail-tab.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetailTab {
  private readonly _dataService = inject(RefTestDetailData);
  protected readonly operationManager = inject(RefTestDetailOperationManager);
  private readonly _destroyRef = inject(DestroyRef);

  protected readonly refTest = this._dataService.refTestData;

  protected readonly RefTestStatus = RefTestStatus;

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

  protected confirmUpdateParticipantDetails(
    input: UpdateRefTestDetailsInput,
    bannerManager: IsolatedBannerManager,
  ): void {
    this.operationManager.updateRefTestDetailsDialog.confirm(
      this._destroyRef,
      input,
      bannerManager,
    );
  }

  protected confirmUpdateConfiguration(
    input: UpdateRefTestConfigurationInput,
    bannerManager: IsolatedBannerManager,
  ): void {
    this.operationManager.updateRefTestConfigurationDialog.confirm(
      this._destroyRef,
      input,
      bannerManager,
    );
  }

  protected confirmUpdateNotificationSettings(
    input: UpdateRefTestNotificationSettingsInput,
    bannerManager: IsolatedBannerManager,
  ): void {
    this.operationManager.updateRefTestNotificationSettingsDialog.confirm(
      this._destroyRef,
      input,
      bannerManager,
    );
  }

  protected extendTime(input: ExtendRefTestTimeInput, bannerManager: IsolatedBannerManager): void {
    this.operationManager.extendRefTestTimeDialog.confirm(this._destroyRef, input, bannerManager);
  }

  protected confirmRegenerateToken(
    input: RegenerateRefTestTokenInput,
    bannerManager: IsolatedBannerManager,
  ): void {
    this.operationManager.regenerateRefTestTokenDialog.confirm(
      this._destroyRef,
      input,
      bannerManager,
    );
  }

  protected confirmReset(options: IResetOptions, bannerManager: IsolatedBannerManager): void {
    this.operationManager.resetDialog.confirm(this._destroyRef, options, bannerManager);
  }

  protected confirmRevive(): void {
    this.operationManager.reviveDialog.confirm(this._destroyRef);
  }
}
