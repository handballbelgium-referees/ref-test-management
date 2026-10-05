import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom, of, Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GetEnabledLanguagesGQL } from '../../../graphql/generated';
import { LanguageConfig } from './language-config';

describe('LanguageConfig', () => {
  let enabledLanguageCodes: string[];
  let browserLanguage: string | undefined;
  let registeredLanguageCodes: string[];
  let fallbackLanguage: string | undefined;
  let usedLanguage: string | undefined;
  let languageChanges: Subject<{ lang: string }>;
  let translateService: {
    onLangChange: Subject<{ lang: string }>;
    getLangs: () => string[];
    addLangs: (languages: string[]) => void;
    setFallbackLang: (language: string) => void;
    getBrowserLang: () => string | undefined;
    use: (language: string) => void;
  };

  beforeEach(() => {
    localStorage.clear();
    enabledLanguageCodes = ['nl', 'fr'];
    browserLanguage = 'en';
    registeredLanguageCodes = [];
    fallbackLanguage = undefined;
    usedLanguage = undefined;
    languageChanges = new Subject<{ lang: string }>();
    translateService = {
      onLangChange: languageChanges,
      getLangs: () => registeredLanguageCodes,
      addLangs: (languages) => registeredLanguageCodes.push(...languages),
      setFallbackLang: (language) => {
        fallbackLanguage = language;
      },
      getBrowserLang: () => browserLanguage,
      use: (language) => {
        usedLanguage = language;
      },
    };

    TestBed.configureTestingModule({
      providers: [
        LanguageConfig,
        {
          provide: GetEnabledLanguagesGQL,
          useValue: {
            fetch: vi.fn(() => of({ data: { enabledLanguages: enabledLanguageCodes } })),
          },
        },
        { provide: TranslateService, useValue: translateService },
      ],
    });
    TestBed.inject(DOCUMENT).documentElement.lang = '';
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.clear();
  });

  it('uses the first enabled language when the saved and browser languages are disabled', async () => {
    localStorage.setItem('app-language', 'en');
    const languageConfig = TestBed.inject(LanguageConfig);

    const initialLanguage = await firstValueFrom(languageConfig.initializeLanguages());

    expect(initialLanguage).toBe('nl');
    expect(languageConfig.initialLanguage).toBe('nl');
    expect(fallbackLanguage).toBe('nl');
    expect(usedLanguage).toBe('nl');
    expect(TestBed.inject(DOCUMENT).documentElement.lang).toBe('nl');
    expect(registeredLanguageCodes).toEqual(['nl', 'fr']);
  });

  it('updates the document language when the selected translation changes', async () => {
    const languageConfig = TestBed.inject(LanguageConfig);
    await firstValueFrom(languageConfig.initializeLanguages());

    languageChanges.next({ lang: 'fr' });

    expect(TestBed.inject(DOCUMENT).documentElement.lang).toBe('fr');
  });
});
