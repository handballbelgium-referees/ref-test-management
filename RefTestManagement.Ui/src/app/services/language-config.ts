import { DOCUMENT } from '@angular/common';
import { DestroyRef, Service, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, first, map, of, shareReplay, switchMap } from 'rxjs';
import { GetEnabledLanguagesGQL } from '../../../graphql/generated';

export type Language = 'en' | 'nl' | 'fr' | 'de';

export interface ILanguageInfo {
  code: Language;
  name: string;
}

// Default fallback languages
const DEFAULT_LANGUAGES: ILanguageInfo[] = [
  { code: 'en', name: 'English' },
  { code: 'nl', name: 'Nederlands' },
  { code: 'fr', name: 'Français' },
  { code: 'de', name: 'Deutsch' },
];

export const LANGUAGE_NAMES: Record<Language, string> = {
  en: 'English',
  nl: 'Nederlands',
  fr: 'Français',
  de: 'Deutsch',
};

@Service()
export class LanguageConfig {
  private readonly _document = inject(DOCUMENT);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _getEnabledLanguagesGQL = inject(GetEnabledLanguagesGQL);
  private readonly _translate = inject(TranslateService);
  private _availableLanguages$: Observable<ILanguageInfo[]> | undefined;
  private _initialLanguage: Language | undefined;

  constructor() {
    this._translate.onLangChange
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe(({ lang }) => {
        if (this._translate.getLangs().includes(lang)) {
          this._document.documentElement.lang = lang;
        }
      });
  }

  get initialLanguage(): Language {
    if (!this._initialLanguage) {
      throw new Error('The initial language has not been resolved.');
    }
    return this._initialLanguage;
  }

  getAvailableLanguages(): Observable<ILanguageInfo[]> {
    if (!this._availableLanguages$) {
      this._availableLanguages$ = this._getEnabledLanguagesGQL.fetch().pipe(
        first(),
        map((result) => {
          const enabledLangs = result.data?.enabledLanguages || [];
          return enabledLangs
            .filter((lang): lang is Language => this.isValidLanguage(lang))
            .map((lang) => ({
              code: lang,
              name: LANGUAGE_NAMES[lang],
            }));
        }),
        catchError(() => {
          return of(DEFAULT_LANGUAGES);
        }),
        shareReplay(1),
      );
    }
    return this._availableLanguages$;
  }

  initializeLanguages(): Observable<Language> {
    return this.getAvailableLanguages().pipe(
      switchMap((languages) => {
        const langCodes = languages.map((l) => l.code);
        const firstEnabledLanguage = langCodes[0];
        if (!firstEnabledLanguage) {
          throw new Error('At least one language must be enabled.');
        }

        // Configure available languages - only add if not already present to avoid duplicates
        const currentLangs = this._translate.getLangs();
        const newLangs = langCodes.filter((lang) => !currentLangs.includes(lang));
        if (newLangs.length > 0) {
          this._translate.addLangs(newLangs);
        }
        this._translate.setFallbackLang(firstEnabledLanguage);

        // Set initial language from localStorage or browser
        const savedLang = localStorage.getItem('app-language');
        const browserLang = this._translate.getBrowserLang();
        const initialLanguage =
          langCodes.find((lang) => lang === savedLang) ??
          langCodes.find((lang) => lang === browserLang) ??
          firstEnabledLanguage;

        this._initialLanguage = initialLanguage;
        this._document.documentElement.lang = initialLanguage;
        return this._translate.use(initialLanguage).pipe(map(() => initialLanguage));
      }),
    );
  }

  private isValidLanguage(lang: string): lang is Language {
    return ['en', 'nl', 'fr', 'de'].includes(lang);
  }
}
