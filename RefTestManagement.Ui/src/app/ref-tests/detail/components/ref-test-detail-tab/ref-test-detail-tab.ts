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
import { GetScoreConfigurationGQL, RefTestStatus } from '../../../../../../graphql/generated';
import { RefTestDetailDataService } from '../../services/ref-test-detail-data.service';
import { EditConfigurationDialog } from './components/dialogs/edit-configuration-dialog/edit-configuration-dialog';
import { EditNotificationSettingsDialog } from './components/dialogs/edit-notification-settings-dialog/edit-notification-settings-dialog';
import { EditParticipantDialog } from './components/dialogs/edit-participant-dialog/edit-participant-dialog';
import { ExtendTimeDialog } from './components/dialogs/extend-time-dialog/extend-time-dialog';
import { RegenerateTokenDialog } from './components/dialogs/regenerate-token-dialog/regenerate-token-dialog';
import { ResetRefTestDialog } from './components/dialogs/reset-ref-test-dialog/reset-ref-test-dialog';
import { ReviveRefTestDialog } from './components/dialogs/revive-ref-test-dialog/revive-ref-test-dialog';
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
    EditParticipantDialog,
    EditConfigurationDialog,
    EditNotificationSettingsDialog,
    ExtendTimeDialog,
    RegenerateTokenDialog,
    ResetRefTestDialog,
    ReviveRefTestDialog,
  ],
  templateUrl: './ref-test-detail-tab.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefTestDetailTab {
  private readonly _dataService = inject(RefTestDetailDataService);
  protected readonly refTest = this._dataService.refTest;

  protected readonly RefTestStatus = RefTestStatus;

  protected readonly editDialog = viewChild(EditParticipantDialog);
  protected readonly showEditDialog = signal(false);

  protected readonly editConfigDialog = viewChild(EditConfigurationDialog);
  protected readonly showEditConfigDialog = signal(false);

  protected readonly editNotificationDialog = viewChild(EditNotificationSettingsDialog);
  protected readonly showEditNotificationDialog = signal(false);

  protected readonly extendTimeDialog = viewChild(ExtendTimeDialog);
  protected readonly showExtendTimeDialog = signal(false);

  protected readonly regenerateTokenDialog = viewChild(RegenerateTokenDialog);
  protected readonly showRegenerateTokenDialog = signal(false);

  protected readonly resetDialog = viewChild(ResetRefTestDialog);
  protected readonly showResetDialog = signal(false);

  protected readonly reviveDialog = viewChild(ReviveRefTestDialog);
  protected readonly showReviveDialog = signal(false);

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

  protected onEditParticipant(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showEditDialog.set(true);
    const dialog = this.editDialog();
    if (dialog) {
      dialog.initialize(refTest.id, refTest.firstName, refTest.lastName, refTest.email);
    }
  }

  protected onEditDialogClose(): void {
    this.showEditDialog.set(false);
  }

  protected onEditConfiguration(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showEditConfigDialog.set(true);
    const dialog = this.editConfigDialog();
    if (dialog) {
      dialog.initialize(
        refTest.id,
        refTest.title ?? null,
        refTest.numberOfQuestions,
        refTest.maxTimeInMinutes,
        refTest.questions?.map((q) => ({ number: q!.number, phrase: q!.phrase! })) ?? [],
      );
    }
  }

  protected onEditConfigDialogClose(): void {
    this.showEditConfigDialog.set(false);
  }

  protected onEditNotificationSettings(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showEditNotificationDialog.set(true);
    const dialog = this.editNotificationDialog();
    if (dialog) {
      dialog.initialize(
        refTest.id,
        refTest.status,
        refTest.invitationSent,
        refTest.resultsSent,
        refTest.sendInvitationsAutomatically,
        refTest.sendResultsAutomatically,
      );
    }
  }

  protected onEditNotificationDialogClose(): void {
    this.showEditNotificationDialog.set(false);
  }

  protected onExtendTime(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showExtendTimeDialog.set(true);
    const dialog = this.extendTimeDialog();
    if (dialog) {
      dialog.initialize(refTest.id, refTest.maxTimeInMinutes);
    }
  }

  protected onExtendTimeDialogClose(): void {
    this.showExtendTimeDialog.set(false);
  }

  protected onRegenerateToken(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showRegenerateTokenDialog.set(true);
    const dialog = this.regenerateTokenDialog();
    if (dialog) {
      dialog.initialize(refTest.id);
    }
  }

  protected onRegenerateTokenDialogClose(): void {
    this.showRegenerateTokenDialog.set(false);
  }

  protected onReset(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showResetDialog.set(true);
    const dialog = this.resetDialog();
    if (dialog) {
      dialog.initialize(refTest.id);
    }
  }

  protected onResetDialogClose(): void {
    this.showResetDialog.set(false);
  }

  protected onRevive(): void {
    const refTest = this.refTest();
    if (!refTest) return;

    this.showReviveDialog.set(true);
    const dialog = this.reviveDialog();
    if (dialog) {
      dialog.initialize(refTest.id);
    }
  }

  protected onReviveDialogClose(): void {
    this.showReviveDialog.set(false);
  }
}
