import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withXhr } from '@angular/common/http';
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
import { ErrorReporter } from './services/error-reporter';

import { provideServiceWorker } from '@angular/service-worker';
import { ApolloLink, CombinedGraphQLErrors, InMemoryCache } from '@apollo/client';
import { ErrorLink } from '@apollo/client/link/error';
import { RetryLink } from '@apollo/client/link/retry';
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
    provideHttpClient(withXhr()),
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
    provideApollo(
      () => {
        const httpLink = inject(HttpLink);
        const reporter = inject(ErrorReporter);

        // HTTP link for queries and mutations
        const http = httpLink.create({
          uri: '/graphql',
          withCredentials: true,
        });

        // One shared SSE client for all subscription operations.
        // retryAttempts: Infinity ensures it keeps reconnecting if the connection
        // drops (e.g. network blip or server restart).
        const sseClient = createClient({
          url: '/graphql',
          credentials: 'include',
          // Prevent the Angular Service Worker from intercepting SSE connections,
          // which would break subscriptions in PWA mode.
          headers: {
            'ngsw-bypass': 'true',
          },
          retryAttempts: Infinity,
          retry: async (retries: number) => {
            // Exponential backoff capped at 10 s
            await new Promise((resolve) =>
              setTimeout(resolve, Math.min(1_000 * 2 ** retries, 10_000)),
            );
          },
        });

        // Create a custom link for SSE subscriptions
        const sseLink = {
          request: (operation: any) => {
            // Return an Observable, not a Promise (Apollo requires Observable)
            return new Observable((observer) => {
              let unsubscribe: (() => void) | null = null;

              const start = () => {
                // Cancel any in-flight attempt before starting a fresh one
                unsubscribe?.();
                unsubscribe = sseClient.subscribe(
                  {
                    query: operation.query.loc?.source.body || '',
                    variables: operation.variables,
                  },
                  {
                    next: (data) => observer.next(data),
                    error: (err) => observer.error(err),
                    complete: () => observer.complete(),
                  },
                );
              };

              // When the PWA returns from the background the SSE connection may be
              // dead. Force a fresh subscribe so events are never missed.
              const onVisibilityChange = () => {
                if (document.visibilityState === 'visible') start();
              };

              document.addEventListener('visibilitychange', onVisibilityChange);
              start();

              // Return cleanup function
              return () => {
                document.removeEventListener('visibilitychange', onVisibilityChange);
                unsubscribe?.();
              };
            });
          },
        };

        // Retries automatically on network errors for everything. Queries also retry on
        // GraphQL execution errors since re-running a read is always safe, and so does
        // StartRefTest specifically (it just returns the already-started session). Other
        // mutations are left alone on GraphQL errors since they may not be idempotent.
        const retryLink = new RetryLink({
          delay: { initial: 300, max: 3000, jitter: true },
          attempts: (count, operation, error) => {
            if (count > 3) return false;
            if (operation.operationName === 'StartRefTest') return true;
            const definition = getMainDefinition(operation.query);
            const isQuery =
              definition.kind === Kind.OPERATION_DEFINITION &&
              definition.operation === OperationTypeNode.QUERY;
            return isQuery || !CombinedGraphQLErrors.is(error);
          },
        });

        // Every GraphQL and network failure passes through here on its way back up the chain, so
        // this is the one place that knows an operation failed. It only observes — returning a
        // value would retry the operation, which is RetryLink's job. Components still handle their
        // own errors; this exists so a failure nobody handled is not completely invisible.
        //
        // The operation name is safe to record because it is developer-authored. The error itself
        // is not, so it goes through ErrorReporter, which withholds the contents in production.
        const errorLink = new ErrorLink(({ error, operation }) => {
          reporter.report(`graphql:${operation.operationName ?? 'anonymous'}`, error);
        });

        // Split link: use SSE for subscriptions, http (with retry) for everything else
        const transport = ApolloLink.split(
          ({ query }) => {
            const definition = getMainDefinition(query);
            return (
              definition.kind === Kind.OPERATION_DEFINITION &&
              definition.operation === OperationTypeNode.SUBSCRIPTION
            );
          },
          sseLink as any,
          ApolloLink.from([retryLink, http]),
        );

        // errorLink sits above the split so subscription failures are reported too, and above
        // retryLink so an operation is reported once after its retries are exhausted rather than
        // once per failed attempt.
        const link = ApolloLink.from([errorLink, transport]);

        return {
          link,
          cache: new InMemoryCache({
            typePolicies: {
              Query: {
                fields: {
                  refTests: relayStylePagination(['where', 'order']),
                  auditLogs: relayStylePagination(['where', 'order']),
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
