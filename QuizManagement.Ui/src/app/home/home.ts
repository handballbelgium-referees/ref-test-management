import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Auth } from '../auth/services/auth';

@Component({
  selector: 'app-home',
  imports: [TranslatePipe],
  templateUrl: './home.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  private readonly _auth = inject(Auth);

  protected readonly isLoggedIn = computed(() => !!this._auth.isAuthenticated());

  protected login(): void {
    this._auth.login();
  }
}
