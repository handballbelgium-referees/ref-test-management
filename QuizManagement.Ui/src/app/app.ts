import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Auth } from './auth/services/auth';

type Language = 'en' | 'nl' | 'fr' | 'de';

interface LanguageInfo {
  code: Language;
  name: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, TranslatePipe],
  templateUrl: './app.html',
  styleUrl: './app.css',
  host: {
    '(document:click)': 'onDocumentClick()',
  },
})
export class App implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(Auth);

  protected readonly isLoggedIn = computed(() => !!this.auth.isAuthenticated());
  protected readonly user = this.auth.user;

  protected readonly showLanguageMenu = signal(false);
  protected readonly availableLanguages: LanguageInfo[] = [
    { code: 'en', name: 'English' },
    { code: 'nl', name: 'Nederlands' },
    { code: 'fr', name: 'Français' },
    { code: 'de', name: 'Deutsch' },
  ];

  ngOnInit(): void {
    this.translate.addLangs(['en', 'nl', 'fr', 'de']);
    const browserLang = this.translate.getBrowserLang();
    const defaultLang =
      browserLang && ['en', 'nl', 'fr', 'de'].includes(browserLang) ? browserLang : 'en';
    this.translate.use(defaultLang);
  }

  protected get currentLocale(): Language {
    return (this.translate.getCurrentLang() as Language) || 'en';
  }

  protected setLanguage(lang: Language): void {
    this.translate.use(lang);
    this.showLanguageMenu.set(false);
  }

  protected toggleLanguageMenu(event: Event): void {
    event.stopPropagation();
    this.showLanguageMenu.update((v) => !v);
  }

  protected onDocumentClick(): void {
    if (this.showLanguageMenu()) {
      this.showLanguageMenu.set(false);
    }
  }

  protected login(): void {
    this.auth.login();
  }

  protected logout(): void {
    this.auth.logout();
  }
}
