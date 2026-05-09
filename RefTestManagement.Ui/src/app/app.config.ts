import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import {
  ApplicationConfig,
  ErrorHandler,
  inject,
  isDevMode,
  LOCALE_ID,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { GlobalErrorHandler } from './services/global-error-handler';

import { provideServiceWorker } from '@angular/service-worker';
import { ApolloLink, InMemoryCache } from '@apollo/client';
import { getMainDefinition, relayStylePagination } from '@apollo/client/utilities';
import { provideApollo } from 'apollo-angular';
import { HttpLink } from 'apollo-angular/http';
import { Kind, OperationTypeNode } from 'graphql';
import { createClient } from 'graphql-sse';
import { forkJoin, from, Observable, of, switchMap, tap } from 'rxjs';
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
                tap((localeModule) => registerLocaleData(localeModule.default, `${langCode}-BE`)),
              )
            : null;
        })
        .filter((obs) => obs !== null);

      return imports.length > 0 ? forkJoin(imports) : of([]);
    }),
  );
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    provideAppInitializer(() => {
      const languageConfigService = inject(LanguageConfig);
      return registerDynamicLocales().pipe(
        switchMap(() => languageConfigService.initializeLanguages()),
      );
    }),
    {
      provide: LOCALE_ID,
      useFactory: () => {
        const translate = inject(TranslateService);
        const currentLang =
          translate.getCurrentLang() || localStorage.getItem('app-language') || 'en';
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

        // HTTP link for queries and mutations
        const http = httpLink.create({
          uri: '/graphql',
          withCredentials: true,
        });

        // Create a custom link for SSE subscriptions
        const sseLink = {
          request: (operation: any) => {
            // Return an Observable, not a Promise (Apollo requires Observable)
            return new Observable((observer) => {
              const client = createClient({
                url: '/graphql',
                credentials: 'include',
                // Prevent the Angular Service Worker from intercepting SSE connections,
                // which would break subscriptions in PWA mode.
                headers: {
                  'ngsw-bypass': 'true',
                },
              });

              const unsubscribe = client.subscribe(
                {
                  query: operation.query.loc?.source.body || '',
                  variables: operation.variables,
                },
                {
                  next: (data) => {
                    observer.next(data);
                  },
                  error: (err) => {
                    observer.error(err);
                  },
                  complete: () => {
                    observer.complete();
                  },
                },
              );

              // Return cleanup function
              return () => unsubscribe();
            });
          },
        };

        // Split link: use SSE for subscriptions, http for everything else
        const link = ApolloLink.split(
          ({ query }) => {
            const definition = getMainDefinition(query);
            return (
              definition.kind === Kind.OPERATION_DEFINITION &&
              definition.operation === OperationTypeNode.SUBSCRIPTION
            );
          },
          sseLink as any,
          http,
        );

        return {
          link,
          cache: new InMemoryCache({
            typePolicies: {
              Query: {
                fields: {
                  refTests: relayStylePagination(['where', 'order']),
                },
              },
            },
          }),
        };
      },
      {
        useMutationLoading: true,
      },
    ),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
};
