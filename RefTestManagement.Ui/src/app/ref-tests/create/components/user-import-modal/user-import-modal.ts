import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-user-import-modal',
  imports: [TranslatePipe],
  templateUrl: './user-import-modal.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'host',
  },
})
export class UserImportModal {
  readonly show = input<boolean>(false);

  readonly import = output<string>();
  readonly cancel = output<void>();

  protected readonly text = signal('');

  protected onImport(): void {
    this.import.emit(this.text());
    this.text.set('');
  }

  protected onCancel(): void {
    this.text.set('');
    this.cancel.emit();
  }
}
