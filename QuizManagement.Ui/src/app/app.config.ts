import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  isDevMode,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { provideServiceWorker } from '@angular/service-worker';
import { InMemoryCache } from '@apollo/client';
import { relayStylePagination } from '@apollo/client/utilities';
import { provideApollo } from 'apollo-angular';
import { HttpLink } from 'apollo-angular/http';
import { forkJoin, from, of, switchMap, tap } from 'rxjs';
import { routes } from './app.routes';
import { LanguageConfig } from './services/language-config';

// Dynamic locale registration helper
function registerDynamicLocales() {
  const languageConfigService = inject(LanguageConfig);

  return languageConfigService.getAvailableLanguages().pipe(
    switchMap((enabledLanguages) => {
      const localeMap = {
        en: () => from(import('@angular/common/locales/en')),
        nl: () => from(import('@angular/common/locales/nl')),
        fr: () => from(import('@angular/common/locales/fr')),
        de: () => from(import('@angular/common/locales/de')),
      };

      const imports = enabledLanguages
        .map((langInfo) => {
          const langCode = langInfo.code;
          return localeMap[langCode as keyof typeof localeMap]
            ? localeMap[langCode as keyof typeof localeMap]().pipe(
                tap((localeModule) => registerLocaleData(localeModule.default, `${langCode}-BE`))
              )
            : null;
        })
        .filter((obs) => obs !== null);

      return imports.length > 0 ? forkJoin(imports) : of([]);
    })
  );
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    provideAppInitializer(() => {
      const languageConfigService = inject(LanguageConfig);
      return registerDynamicLocales().pipe(
        switchMap(() => languageConfigService.initializeLanguages())
      );
    }),
    {
      provide: LOCALE_ID,
      useFactory: () => {
        const translate = inject(TranslateService);
        const currentLang = translate.currentLang || localStorage.getItem('app-language') || 'en';
        return `${currentLang}-BE`;
      },
    },
    provideTranslateService({
      fallbackLang: 'en',
      loader: provideTranslateHttpLoader({
        prefix: '/i18n/',
        suffix: '.json',
        enforceLoading: true,
      }),
    }),
    provideHttpClient(),
    provideApollo(
      () => {
        const httpLink = inject(HttpLink);

        return {
          link: httpLink.create({
            uri: '/graphql',
            withCredentials: true,
          }),
          cache: new InMemoryCache({
            typePolicies: {
              Query: {
                fields: {
                  quizSessions: relayStylePagination(['where', 'order']),
                },
              },
            },
          }),
        };
      },
      {
        useMutationLoading: true,
      }
    ),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
