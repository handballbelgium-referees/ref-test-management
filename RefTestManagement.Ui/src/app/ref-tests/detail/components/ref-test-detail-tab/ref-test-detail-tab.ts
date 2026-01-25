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
import { EditParticipantDialog } from './components/edit-participant-dialog/edit-participant-dialog';
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
}
