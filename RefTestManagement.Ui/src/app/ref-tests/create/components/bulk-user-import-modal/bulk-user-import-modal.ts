import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-bulk-user-import-modal',
  imports: [TranslatePipe],
  templateUrl: './bulk-user-import-modal.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class BulkUserImportModal {
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
