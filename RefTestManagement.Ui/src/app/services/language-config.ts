import { Injectable, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, first, map, of, shareReplay } from 'rxjs';
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

@Injectable({
  providedIn: 'root',
})
export class LanguageConfig {
  private readonly _getEnabledLanguagesGQL = inject(GetEnabledLanguagesGQL);
  private readonly _translate = inject(TranslateService);
  private _availableLanguages$: Observable<ILanguageInfo[]> | undefined;

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
          console.warn('Failed to fetch enabled languages, using defaults');
          return of(DEFAULT_LANGUAGES);
        }),
        shareReplay(1)
      );
    }
    return this._availableLanguages$;
  }

  initializeLanguages(): Observable<string> {
    return this.getAvailableLanguages().pipe(
      map((languages) => {
        const langCodes = languages.map((l) => l.code);

        // Configure available languages - only add if not already present to avoid duplicates
        const currentLangs = this._translate.getLangs();
        const newLangs = langCodes.filter((lang) => !currentLangs.includes(lang));
        if (newLangs.length > 0) {
          this._translate.addLangs(newLangs);
        }
        this._translate.setFallbackLang(langCodes[0] || 'en');

        // Set initial language from localStorage or browser
        const savedLang = localStorage.getItem('app-language');
        let defaultLang: string;

        if (savedLang && langCodes.includes(savedLang as Language)) {
          defaultLang = savedLang;
        } else {
          const browserLang = this._translate.getBrowserLang();
          defaultLang =
            browserLang && langCodes.includes(browserLang as Language) ? browserLang : 'en';
        }

        this._translate.use(defaultLang);
        return defaultLang;
      })
    );
  }

  private isValidLanguage(lang: string): lang is Language {
    return ['en', 'nl', 'fr', 'de'].includes(lang);
  }
}
