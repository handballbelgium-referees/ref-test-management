import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterOutlet } from '@angular/router';
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
export class App {
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(Auth);
  private readonly titleService = inject(Title);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly isLoggedIn = computed(() => !!this.auth.isAuthenticated());
  protected readonly user = this.auth.user;

  protected readonly showLanguageMenu = signal(false);
  protected readonly availableLanguages: LanguageInfo[] = [
    { code: 'en', name: 'English' },
    { code: 'nl', name: 'Nederlands' },
    { code: 'fr', name: 'Français' },
    { code: 'de', name: 'Deutsch' },
  ];

  constructor() {
    this.translate.addLangs(['en', 'nl', 'fr', 'de']);

    // Set initial language from localStorage or browser
    const savedLang = localStorage.getItem('app-language') as Language | null;
    let defaultLang: string;

    if (savedLang && ['en', 'nl', 'fr', 'de'].includes(savedLang)) {
      defaultLang = savedLang;
    } else {
      const browserLang = this.translate.getBrowserLang();
      defaultLang =
        browserLang && ['en', 'nl', 'fr', 'de'].includes(browserLang) ? browserLang : 'en';
    }

    this.translate.use(defaultLang);

    // Subscribe to query params for language changes
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const queryLang = params['lang'] as Language | undefined;
      if (queryLang && ['en', 'nl', 'fr', 'de'].includes(queryLang)) {
        this.translate.use(queryLang);
        localStorage.setItem('app-language', queryLang);
      }
    });

    // Update page title when language changes
    this.translate.onLangChange.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.translate.get('app.pageTitle').subscribe((title: string) => {
        this.titleService.setTitle(title);
      });
    });

    // Set initial title
    this.translate
      .get('app.pageTitle')
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((title: string) => {
        this.titleService.setTitle(title);
      });
  }

  protected get currentLocale(): Language {
    return (this.translate.getCurrentLang() as Language) || 'en';
  }

  protected setLanguage(lang: Language): void {
    this.translate.use(lang);
    localStorage.setItem('app-language', lang);
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
