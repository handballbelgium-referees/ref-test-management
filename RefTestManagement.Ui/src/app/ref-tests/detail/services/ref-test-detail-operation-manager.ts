import { DestroyRef, inject, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import {
  ExtendRefTestTimeInput,
  GetRefTestByIdQuery,
  RegenerateRefTestTokenInput,
  UpdateRefTestConfigurationInput,
  UpdateRefTestDetailsInput,
  UpdateRefTestNotificationSettingsInput,
} from '../../../../../graphql/generated';
import { Banner } from '../../../services/banner';
import { createDialogOperation } from '../../../shared/utils/dialog-utils';
import { IResetOptions } from '../../list/services/types';
import { RefTestData } from '../../services/ref-test-data';
import { RefTestDetailData } from './ref-test-detail-data';

type RefTest = Extract<GetRefTestByIdQuery['refTest'], { __typename: 'RefTest' }>;

@Injectable()
export class RefTestDetailOperationManager {
  // ========================================================================
  // INJECTIONS
  // ========================================================================
  private readonly _dataService = inject(RefTestData);
  private readonly _translateService = inject(TranslateService);
  private readonly _router = inject(Router);
  private readonly _detailDataService = inject(RefTestDetailData);
  private readonly _bannerService = inject(Banner);
  private readonly _destroyRef = inject(DestroyRef);

  // ========================================================================
  // DIALOG OPERATIONS
  // ========================================================================

  readonly deleteDialog = createDialogOperation(
    (ids, _, bannerManager) =>
      this._dataService.deleteRefTests(ids, this._destroyRef, {
        onSuccess: () => {
          this._bannerService.success(
            this._translateService.instant('ref_tests.detail.delete_success'),
          );
          // Navigate back to list after successful delete
          this._router.navigate(['/ref-tests']);
        },
        onError: () => {
          bannerManager?.error(this._translateService.instant('ref_tests.detail.delete_error'));
        },
      }),
    () => [this._detailDataService.refTestId()],
  );

  readonly sendResultDialog = createDialogOperation(
    (ids, _, bannerManager) =>
      this._dataService.sendResults(ids, this._destroyRef, {
        onSuccess: () => {
          this._bannerService.success(
            this._translateService.instant('ref_tests.detail.results_sent'),
          );
        },
        onError: () => {
          bannerManager?.error(this._translateService.instant('ref_tests.detail.results_error'));
        },
      }),
    () => [this._detailDataService.refTestId()],
  );

  readonly sendInvitationDialog = createDialogOperation(
    (ids, _, bannerManager) =>
      this._dataService.sendInvitations(ids, this._destroyRef, {
        onSuccess: () => {
          this._bannerService.success(
            this._translateService.instant('ref_tests.detail.invitation_sent'),
          );
        },
        onError: () => {
          bannerManager?.error(this._translateService.instant('ref_tests.detail.invitation_error'));
        },
      }),
    () => [this._detailDataService.refTestId()],
  );

  readonly resetDialog = createDialogOperation<IResetOptions>(
    (ids, options, bannerManager) =>
      this._dataService.resetRefTests(
        { ids, resetType: options.resetType, regenerateToken: options.regenerateToken },
        this._destroyRef,
        {
          onSuccess: ({ successCount, failedCount }) => {
            if (successCount > 0) {
              this._bannerService.success(
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
      ),
    () => [this._detailDataService.refTestId()],
  );

  readonly reviveDialog = createDialogOperation(
    (ids, _, bannerManager) =>
      this._dataService.reviveRefTests(ids, this._destroyRef, {
        onSuccess: ({ successCount, failedCount }) => {
          if (successCount > 0) {
            this._bannerService.success(
              this._translateService.instant('ref_tests.detail.revive.success'),
            );
          }
          if (failedCount > 0) {
            bannerManager?.error(this._translateService.instant('ref_tests.detail.revive.error'));
          }
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.revive_error')),
      }),
    () => [this._detailDataService.refTestId()],
  );

  readonly approveDialog = createDialogOperation(
    (ids, _, bannerManager) =>
      this._dataService.approveRefTests(ids, this._destroyRef, {
        onSuccess: ({ successCount, failedCount }) => {
          if (successCount > 0) {
            this._bannerService.success(
              this._translateService.instant('ref_tests.list.approve_success', { count: 1 }),
            );
          }
          if (failedCount > 0) {
            bannerManager?.error(this._translateService.instant('ref_tests.list.approve_error'));
          }
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.approve_error')),
      }),
    () => [this._detailDataService.refTestId()],
  );

  readonly rejectDialog = createDialogOperation<string>(
    (ids, reason, bannerManager) =>
      this._dataService.rejectRefTests(ids, reason, this._destroyRef, {
        onSuccess: ({ successCount, failedCount }) => {
          if (successCount > 0) {
            this._bannerService.success(
              this._translateService.instant('ref_tests.list.reject_success', { count: 1 }),
            );
          }
          if (failedCount > 0) {
            bannerManager?.error(this._translateService.instant('ref_tests.list.reject_error'));
          }
        },
        onError: () =>
          bannerManager?.error(this._translateService.instant('ref_tests.list.reject_error')),
      }),
    () => [this._detailDataService.refTestId()],
  );

  readonly updateRefTestDetailsDialog = createDialogOperation<UpdateRefTestDetailsInput, RefTest>(
    (_, updatedData, bannerManager) =>
      this._detailDataService.editParticipantDetails(updatedData, this._destroyRef, {
        onSuccess: () => {
          this._bannerService.success(
            this._translateService.instant('ref_tests.detail.edit_participant.success'),
          );
        },
        onError: () => {
          bannerManager?.error(
            this._translateService.instant('ref_tests.detail.edit_participant.error'),
          );
        },
      }),
  );

  readonly updateRefTestConfigurationDialog = createDialogOperation<
    UpdateRefTestConfigurationInput,
    RefTest
  >((_, updatedData, bannerManager) =>
    this._detailDataService.updateRefTestConfiguration(updatedData, this._destroyRef, {
      onSuccess: () => {
        this._bannerService.success(
          this._translateService.instant('ref_tests.detail.edit_configuration.success'),
        );
      },
      onError: () => {
        bannerManager?.error(
          this._translateService.instant('ref_tests.detail.edit_configuration.error'),
        );
      },
    }),
  );

  readonly updateRefTestNotificationSettingsDialog = createDialogOperation<
    UpdateRefTestNotificationSettingsInput,
    RefTest
  >((_, updatedData, bannerManager) =>
    this._detailDataService.updateRefTestNotificationSettings(updatedData, this._destroyRef, {
      onSuccess: () => {
        this._bannerService.success(
          this._translateService.instant('ref_tests.detail.edit_notification_settings.success'),
        );
      },
      onError: () => {
        bannerManager?.error(
          this._translateService.instant('ref_tests.detail.edit_notification_settings.error'),
        );
      },
    }),
  );

  readonly extendRefTestTimeDialog = createDialogOperation<ExtendRefTestTimeInput, RefTest>(
    (_, updatedData, bannerManager) =>
      this._detailDataService.extendRefTestTime(updatedData, this._destroyRef, {
        onSuccess: () => {
          this._bannerService.success(
            this._translateService.instant('ref_tests.detail.extend_time.success'),
          );
        },
        onError: () => {
          bannerManager?.error(
            this._translateService.instant('ref_tests.detail.extend_time.error'),
          );
        },
      }),
  );

  readonly regenerateRefTestTokenDialog = createDialogOperation<RegenerateRefTestTokenInput>(
    (_, updatedData, bannerManager) =>
      this._detailDataService.regenerateRefTestToken(updatedData, this._destroyRef, {
        onSuccess: () => {
          this._bannerService.success(
            this._translateService.instant('ref_tests.detail.regenerate_token.success'),
          );
        },
        onError: () => {
          bannerManager?.error(
            this._translateService.instant('ref_tests.detail.regenerate_token.error'),
          );
        },
      }),
  );
}
