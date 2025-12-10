import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Auth } from '../auth/services/auth';

@Component({
  selector: 'app-home',
  imports: [TranslatePipe],
  templateUrl: './home.html',
  styleUrl: './home.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Home {
  private readonly auth = inject(Auth);

  protected readonly currentYear = computed(() => new Date().getFullYear());

  protected login(): void {
    this.auth.login();
  }
}
