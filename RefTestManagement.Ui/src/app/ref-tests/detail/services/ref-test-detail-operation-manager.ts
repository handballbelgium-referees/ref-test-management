import { inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import {
  ExtendRefTestTimeInput,
  RefTest,
  RegenerateRefTestTokenInput,
  UpdateRefTestConfigurationInput,
  UpdateRefTestDetailsInput,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../graphql/generated';
import { createDialogOperation } from '../../../shared/utils/dialog-utils';
import { IResetOptions } from '../../list/services/types';
import { RefTestData } from '../../services/ref-test-data';
import { RefTestDetailData } from './ref-test-detail-data';

@Injectable({
  providedIn: 'root',
})
export class RefTestDetailOperationManager {
  // ========================================================================
  // INJECTIONS
  // ========================================================================
  private readonly _dataService = inject(RefTestData);
  private readonly _translateService = inject(TranslateService);
  private readonly _router = inject(Router);
  private readonly _detailDataService = inject(RefTestDetailData);

  // ========================================================================
  // DIALOG OPERATIONS
  // ========================================================================

  readonly deleteDialog = createDialogOperation(
    (ids, destroyRef, _, loading, bannerManager) => {
      this._dataService.deleteRefTests(ids, loading, destroyRef, {
        onSuccess: () => {
          bannerManager?.success(this._translateService.instant('ref_tests.detail.delete_success'));
          // Navigate back to list after successful delete
          this._router.navigate(['/ref-tests']);
        },
        onError: () => {
          bannerManager?.error(this._translateService.instant('ref_tests.detail.delete_error'));
        },
      });
    },
    () => [this._detailDataService.refTestId()],
  );

  readonly sendResultDialog = createDialogOperation(
    (ids, destroyRef, _, loading, bannerManager) => {
      this._dataService.sendResults(ids, loading, destroyRef, {
        onSuccess: () => {
          bannerManager?.success(this._translateService.instant('ref_tests.detail.results_sent'));
        },
        onError: () => {
          bannerManager?.error(this._translateService.instant('ref_tests.detail.results_error'));
        },
      });
    },
    () => [this._detailDataService.refTestId()],
  );

  readonly sendInvitationDialog = createDialogOperation(
    (ids, destroyRef, _, loading, bannerManager) => {
      this._dataService.sendInvitations(ids, loading, destroyRef, {
        onSuccess: () => {
          bannerManager?.success(
            this._translateService.instant('ref_tests.detail.invitation_sent'),
          );
        },
        onError: () => {
          bannerManager?.error(this._translateService.instant('ref_tests.detail.invitation_error'));
        },
      });
    },
    () => [this._detailDataService.refTestId()],
  );

  readonly resetDialog = createDialogOperation<IResetOptions>(
    (ids, destroyRef, options, loading, bannerManager) => {
      this._dataService.resetRefTests(
        { ids, resetType: options.resetType, regenerateToken: options.regenerateToken },
        loading,
        destroyRef,
        {
          onSuccess: ({ successCount, failedCount }) => {
            if (successCount > 0) {
              bannerManager?.success(
                this._translateService.instant('ref_tests.detail.reset.success'),
              );
            }
            if (failedCount > 0) {
              bannerManager?.error(this._translateService.instant('ref_tests.detail.reset.error'));
            }
          },
          onError: () =>
            bannerManager?.error(this._translateService.instant('ref_tests.list.reset_error')),
        },
      );
    },
    () => [this._detailDataService.refTestId()],
  );

  readonly reviveDialog = createDialogOperation(
    (ids, destroyRef, _, loading, bannerManager) => {
      this._dataService.reviveRefTests(ids, loading, destroyRef, {
        onSuccess: ({ successCount, failedCount }) => {
          if (successCount > 0) {
            bannerManager?.success(
              this._translateService.instant('ref_tests.detail.revive.success'),
            );
          }
          if (failedCount > 0) {
            bannerManager?.error(this._translateService.instant('ref_tests.detail.revive.error'));
          }
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.revive_error')),
      });
    },
    () => [this._detailDataService.refTestId()],
  );

  readonly updateRefTestDetailsDialog = createDialogOperation<UpdateRefTestDetailsInput, RefTest>(
    (_, destroyRef, updatedData, loading, bannerManager) => {
      this._detailDataService.editParticipantDetails(updatedData, loading, destroyRef, {
        onSuccess: () => {
          bannerManager?.success(
            this._translateService.instant('ref_tests.detail.edit_participant.success'),
          );
        },
        onError: () => {
          bannerManager?.error(
            this._translateService.instant('ref_tests.detail.edit_participant.error'),
          );
        },
      });
    },
  );

  readonly updateRefTestConfigurationDialog = createDialogOperation<
    UpdateRefTestConfigurationInput,
    RefTest
  >((_, destroyRef, updatedData, loading, bannerManager) => {
    this._detailDataService.updateRefTestConfiguration(updatedData, loading, destroyRef, {
      onSuccess: () => {
        bannerManager?.success(
          this._translateService.instant('ref_tests.detail.edit_configuration.success'),
        );
      },
      onError: () => {
        bannerManager?.error(
          this._translateService.instant('ref_tests.detail.edit_configuration.error'),
        );
      },
    });
  });

  readonly updateRefTestNotificationSettingsDialog = createDialogOperation<
    UpdateRefTestNotificationSettingsInput,
    RefTest
  >((_, destroyRef, updatedData, loading, bannerManager) => {
    this._detailDataService.updateRefTestNotificationSettings(updatedData, loading, destroyRef, {
      onSuccess: () => {
        bannerManager?.success(
          this._translateService.instant('ref_tests.detail.edit_notification_settings.success'),
        );
      },
      onError: () => {
        bannerManager?.error(
          this._translateService.instant('ref_tests.detail.edit_notification_settings.error'),
        );
      },
    });
  });

  readonly extendRefTestTimeDialog = createDialogOperation<ExtendRefTestTimeInput, RefTest>(
    (_, destroyRef, updatedData, loading, bannerManager) => {
      this._detailDataService.extendRefTestTime(updatedData, loading, destroyRef, {
        onSuccess: () => {
          bannerManager?.success(
            this._translateService.instant('ref_tests.detail.extend_time.success'),
          );
        },
        onError: () => {
          bannerManager?.error(
            this._translateService.instant('ref_tests.detail.extend_time.error'),
          );
        },
      });
    },
  );

  readonly regenerateRefTestTokenDialog = createDialogOperation<RegenerateRefTestTokenInput>(
    (_, destroyRef, updatedData, loading, bannerManager) => {
      this._detailDataService.regenerateRefTestToken(updatedData, loading, destroyRef, {
        onSuccess: () => {
          bannerManager?.success(
            this._translateService.instant('ref_tests.detail.regenerate_token.success'),
          );
        },
        onError: () => {
          bannerManager?.error(
            this._translateService.instant('ref_tests.detail.regenerate_token.error'),
          );
        },
      });
    },
  );
}
