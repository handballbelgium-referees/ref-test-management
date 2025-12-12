import { Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { filter, map } from 'rxjs';
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
  host: {
    '(document:click)': 'onDocumentClick()',
  },
})
export class App {
  private readonly _translate = inject(TranslateService);
  private readonly _auth = inject(Auth);
  private readonly _titleService = inject(Title);
  private readonly _route = inject(ActivatedRoute);
  private readonly _router = inject(Router);
  private readonly _destroyRef = inject(DestroyRef);

  private readonly _isQuizRoute = toSignal(
    this._router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this._router.url.startsWith('/quiz')),
      takeUntilDestroyed(this._destroyRef)
    ),
    { initialValue: this._router.url.startsWith('/quiz') }
  );

  protected readonly isLoggedIn = computed(() => !!this._auth.isAuthenticated());
  protected readonly user = this._auth.user;
  protected readonly showAuthUI = computed(() => !this._isQuizRoute());

  protected readonly showLanguageMenu = signal(false);
  protected readonly availableLanguages: LanguageInfo[] = [
    { code: 'en', name: 'English' },
    { code: 'nl', name: 'Nederlands' },
    { code: 'fr', name: 'Français' },
    { code: 'de', name: 'Deutsch' },
  ];

  constructor() {
    this._translate.addLangs(['en', 'nl', 'fr', 'de']);

    // Set initial language from localStorage or browser
    const savedLang = localStorage.getItem('app-language') as Language | null;
    let defaultLang: string;

    if (savedLang && ['en', 'nl', 'fr', 'de'].includes(savedLang)) {
      defaultLang = savedLang;
    } else {
      const browserLang = this._translate.getBrowserLang();
      defaultLang =
        browserLang && ['en', 'nl', 'fr', 'de'].includes(browserLang) ? browserLang : 'en';
    }

    this._translate.use(defaultLang);

    // Subscribe to query params for language changes
    this._route.queryParams.pipe(takeUntilDestroyed(this._destroyRef)).subscribe((params) => {
      const queryLang = params['lang'] as Language | undefined;
      if (queryLang && ['en', 'nl', 'fr', 'de'].includes(queryLang)) {
        this._translate.use(queryLang);
        localStorage.setItem('app-language', queryLang);
      }
    });

    // Update page title when language changes or route changes
    this._translate.onLangChange.pipe(takeUntilDestroyed(this._destroyRef)).subscribe(() => {
      this.updateTitle();
    });

    // Set initial title
    this.updateTitle();

    // Update title when route changes
    effect(() => {
      this._isQuizRoute();
      this.updateTitle();
    });
  }

  private updateTitle(): void {
    const titleKey = this._isQuizRoute() ? 'quiz.page_title' : 'app.pageTitle';
    this._translate.get(titleKey).subscribe((title: string) => {
      this._titleService.setTitle(title);
    });
  }

  protected get currentLocale(): Language {
    return (this._translate.getCurrentLang() as Language) || 'en';
  }

  protected setLanguage(lang: Language): void {
    this._translate.use(lang);
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
    this._auth.login();
  }

  protected logout(): void {
    this._auth.logout();
  }

  protected getInitials(name: string): string {
    return name
      .split(' ')
      .map((part) => part[0])
      .join('')
      .toUpperCase()
      .slice(0, 2);
  }
}
