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
      this._permissions.hasPermission(Permissions.RefTests.Create) ||
      this._permissions.hasPermission(Permissions.RefTests.ViewList) ||
      this._permissions.hasPermission(Permissions.AuditLogs.View) ||
      this._permissions.hasPermission(Permissions.PrivacyOperations.ReviewWithdrawals),
  );

  protected readonly languageCount = computed(() => this._translate.getLangs().length);
  protected readonly languageList = computed(() =>
    this._translate
      .getLangs()
      .map((lang) => LANGUAGE_NAMES[lang as keyof typeof LANGUAGE_NAMES])
      .join(', '),
  );

  protected readonly isLoggedIn = computed(
    () => this._auth.authenticationState().status === 'authenticated',
  );

  protected readonly hasAuthenticationCheckError = computed(
    () => this._auth.authenticationState().status === 'error',
  );

  protected readonly isLoading = computed(() => {
    const authenticationState = this._auth.authenticationState();
    if (authenticationState.status === 'checking') return true;
    if (
      authenticationState.status === 'authenticated' &&
      this._permissions.permissions() === undefined
    ) {
      return true;
    }
    return false;
  });

  protected retryAuthenticationCheck(): void {
    this._auth.retryAuthenticationCheck();
  }

  protected login(): void {
    this._auth.login();
  }
}
