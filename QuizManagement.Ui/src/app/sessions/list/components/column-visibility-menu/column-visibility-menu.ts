import { ChangeDetectionStrategy, Component, HostListener, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-column-visibility-menu',
  imports: [TranslatePipe],
  templateUrl: './column-visibility-menu.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ColumnVisibilityMenu {
  readonly visibleColumns = input.required<Set<string>>();
  readonly showMenu = input.required<boolean>();

  readonly toggleMenu = output<void>();
  readonly toggleColumn = output<string>();

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected onToggleMenu(): void {
    this.toggleMenu.emit();
  }

  protected onToggleColumn(column: string): void {
    this.toggleColumn.emit(column);
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.column-menu-container')) {
      if (this.showMenu()) {
        this.toggleMenu.emit();
      }
    }
  }
}
