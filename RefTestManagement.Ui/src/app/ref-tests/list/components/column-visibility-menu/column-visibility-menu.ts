import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-column-visibility-menu',
  imports: [TranslatePipe],
  templateUrl: './column-visibility-menu.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'block',
  },
})
export class ColumnVisibilityMenu {
  private readonly document = inject(DOCUMENT);

  readonly visibleColumns = input.required<Set<string>>();
  readonly showMenu = input.required<boolean>();

  readonly toggleMenu = output<void>();
  readonly toggleColumn = output<string>();

  constructor() {
    // Set up document click listener using effect instead of @HostListener
    effect(() => {
      if (this.showMenu()) {
        const handleClick = (event: MouseEvent) => {
          const target = event.target as HTMLElement;
          if (!target.closest('.column-menu-container')) {
            this.toggleMenu.emit();
          }
        };

        // Add listener with a slight delay to avoid immediate triggering
        const timeoutId = setTimeout(() => {
          this.document.addEventListener('click', handleClick, { once: true });
        }, 0);

        return () => {
          clearTimeout(timeoutId);
          this.document.removeEventListener('click', handleClick);
        };
      }
      return undefined;
    });
  }

  protected isColumnVisible(column: string): boolean {
    return this.visibleColumns().has(column);
  }

  protected onToggleMenu(): void {
    this.toggleMenu.emit();
  }

  protected onToggleColumn(column: string): void {
    this.toggleColumn.emit(column);
  }
}
