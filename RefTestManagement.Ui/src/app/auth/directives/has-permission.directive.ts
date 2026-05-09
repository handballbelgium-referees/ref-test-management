import {
  computed,
  Directive,
  EmbeddedViewRef,
  inject,
  input,
  TemplateRef,
  ViewContainerRef,
  effect,
} from '@angular/core';
import { PermissionsService } from '../services/permissions';

/**
 * Structural directive that conditionally renders its host element based on whether
 * the current user holds a specific task permission.
 *
 * Usage:
 * ```html
 * <button *hasPermission="Permissions.RefTests.Create">Create</button>
 * ```
 *
 * Namespace wildcards and the superadmin role are handled by {@link PermissionsService}.
 */
@Directive({
  selector: '[hasPermission]',
  standalone: true,
})
export class HasPermission {
  private readonly _template = inject(TemplateRef);
  private readonly _viewContainer = inject(ViewContainerRef);
  private readonly _permissions = inject(PermissionsService);

  readonly hasPermission = input.required<string>();

  private _view: EmbeddedViewRef<unknown> | null = null;

  private readonly _hasAccess = computed(() =>
    this._permissions.hasPermission(this.hasPermission()),
  );

  constructor() {
    effect(() => {
      if (this._hasAccess()) {
        if (!this._view) {
          this._view = this._viewContainer.createEmbeddedView(this._template);
        }
      } else {
        this._viewContainer.clear();
        this._view = null;
      }
    });
  }
}
