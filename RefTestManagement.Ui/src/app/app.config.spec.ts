import { FactoryProvider, LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { appConfig } from './app.config';
import { LanguageConfig } from './services/language-config';

describe('app locale configuration', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('derives LOCALE_ID from the resolved language', () => {
    const localeIdProvider = appConfig.providers?.find(
      (provider): provider is FactoryProvider =>
        typeof provider === 'object' &&
        provider !== null &&
        'provide' in provider &&
        provider.provide === LOCALE_ID,
    );

    if (!localeIdProvider) {
      throw new Error('LOCALE_ID provider is not configured.');
    }

    TestBed.configureTestingModule({
      providers: [
        localeIdProvider,
        { provide: LanguageConfig, useValue: { initialLanguage: 'fr' } },
      ],
    });

    expect(TestBed.inject(LOCALE_ID)).toBe('fr-BE');
  });
});
