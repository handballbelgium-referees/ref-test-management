import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { form, FormField, min, required } from '@angular/forms/signals';
import { TranslatePipe } from '@ngx-translate/core';
import { ExtendRefTestTimeInput, RefTest } from '../../../../../../../../../graphql/generated';
import {
  Banner as BannerService,
  IsolatedBannerManager,
} from '../../../../../../../services/banner';
import { Banner } from '../../../../../../../shared/components/banner/banner';

interface IExtendTimeData {
  id: string;
  additionalMinutes: number;
  currentMaxTime: number;
}

@Component({
  selector: 'app-extend-time-dialog',
  imports: [TranslatePipe, FormField, Banner],
  templateUrl: './extend-time-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtendTimeDialog {
  // Create isolated banner manager for this dialog
  protected readonly bannerManager = inject(BannerService).createIsolated();

  protected readonly extendTimeModel = signal<IExtendTimeData>({
    id: '',
    additionalMinutes: 15,
    currentMaxTime: 0,
  });

  protected readonly extendTimeForm = form(this.extendTimeModel, (schema) => {
    required(schema.additionalMinutes, {
      message: 'ref_tests.detail.extend_time.additional_minutes_required',
    });
    min(schema.additionalMinutes, 1, {
      message: 'ref_tests.detail.extend_time.additional_minutes_min',
    });
  });

  readonly loading = input.required<boolean>();
  readonly show = input.required<boolean>();
  protected readonly confirm = output<{
    input: ExtendRefTestTimeInput;
    bannerManager: IsolatedBannerManager;
  }>();
  readonly cancel = output<void>();

  protected readonly canSave = computed(() => {
    return this.extendTimeForm().valid() && !this.loading();
  });

  protected readonly newMaxTime = computed(() => {
    return (this.initialData()?.maxTimeInMinutes ?? 0) + this.extendTimeModel().additionalMinutes;
  });

  constructor() {
    effect(() => {
      if (this.show()) {
        document.body.style.overflow = 'hidden';
      } else {
        document.body.style.overflow = '';
      }
    });

    effect(() => {
      const data = this.initialData();
      if (!data) return;
      this.extendTimeModel.set({
        id: data.id,
        additionalMinutes: 15,
        currentMaxTime: data.maxTimeInMinutes,
      });
    });
  }

  protected onConfirm(): void {
    if (this.extendTimeForm().invalid()) {
      this.extendTimeForm().markAsTouched();
      return;
    }

    const data = this.extendTimeModel();

    this.confirm.emit({
      input: { id: data.id, additionalMinutes: data.additionalMinutes },
      bannerManager: this.bannerManager,
    });
  }
}
