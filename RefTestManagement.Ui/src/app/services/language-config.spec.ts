import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';
import { GetEnabledLanguagesGQL } from '../../../graphql/generated';
import { LanguageConfig } from './language-config';

describe('LanguageConfig', () => {
  let currentLanguage: string;

  beforeEach(() => {
    currentLanguage = 'en';
    TestBed.configureTestingModule({
      providers: [
        LanguageConfig,
        {
          provide: TranslateService,
          useValue: { getCurrentLang: () => currentLanguage },
        },
        {
          provide: GetEnabledLanguagesGQL,
          useValue: {},
        },
      ],
    });
  });

  it('returns the current supported language', () => {
    currentLanguage = 'fr';

    expect(TestBed.inject(LanguageConfig).getCurrentLanguage()).toBe('fr');
  });

  it('falls back to English for an unsupported current language', () => {
    currentLanguage = 'es';

    expect(TestBed.inject(LanguageConfig).getCurrentLanguage()).toBe('en');
  });
});
