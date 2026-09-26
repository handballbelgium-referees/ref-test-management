import { Component, computed, inject } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { HasPermission } from '../auth/directives/has-permission.directive';
import { Permissions } from '../auth/models/permissions';
import { Auth } from '../auth/services/auth';
import { PermissionsService } from '../auth/services/permissions';
import { LANGUAGE_NAMES } from '../services/language-config';
import { Banner } from '../shared/components/banner/banner';

@Component({
  selector: 'app-home',
  imports: [TranslatePipe, Banner, HasPermission],
  templateUrl: './home.html',
  host: {
    class: 'block',
  },
})
export class Home {
  private readonly _auth = inject(Auth);
  private readonly _translate = inject(TranslateService);
  private readonly _permissions = inject(PermissionsService);

  protected readonly Permissions = Permissions;

  protected readonly hasQuickActions = computed(
    () =>
      this._permissions.hasPermission(Permissions.Participants.ViewList) ||
      this._permissions.hasPermission(Permissions.RefTests.Create) ||
      this._permissions.hasPermission(Permissions.RefTests.ViewList) ||
      this._permissions.hasPermission(Permissions.AuditLogs.View),
  );

  protected readonly languageCount = computed(() => this._translate.getLangs().length);
  protected readonly languageList = computed(() =>
    this._translate
      .getLangs()
      .map((lang) => LANGUAGE_NAMES[lang as keyof typeof LANGUAGE_NAMES])
      .join(', '),
  );

  protected readonly isLoggedIn = computed(() => !!this._auth.isAuthenticated());

  protected readonly isLoading = computed(() => {
    const isAuthenticated = this._auth.isAuthenticated();
    if (isAuthenticated === undefined) return true;
    if (isAuthenticated && this._permissions.permissions() === undefined) return true;
    return false;
  });

  protected login(): void {
    this._auth.login();
  }
}
