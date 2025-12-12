import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-bulk-question-import-modal',
  imports: [TranslatePipe],
  templateUrl: './bulk-question-import-modal.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BulkQuestionImportModal {
  readonly show = input<boolean>(false);

  readonly import = output<string>();
  readonly cancel = output<void>();

  protected readonly bulkText = signal('');

  protected onImport(): void {
    this.import.emit(this.bulkText());
    this.bulkText.set('');
  }

  protected onCancel(): void {
    this.bulkText.set('');
    this.cancel.emit();
  }
}
