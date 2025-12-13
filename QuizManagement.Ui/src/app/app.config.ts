import { registerLocaleData } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import localeDe from '@angular/common/locales/de';
import localeEn from '@angular/common/locales/en';
import localeFr from '@angular/common/locales/fr';
import localeNl from '@angular/common/locales/nl';
import {
  ApplicationConfig,
  inject,
  Injector,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

import { InMemoryCache } from '@apollo/client';
import { relayStylePagination } from '@apollo/client/utilities';
import { provideApollo } from 'apollo-angular';
import { HttpLink } from 'apollo-angular/http';
import { routes } from './app.routes';

// Register locale data for date formatting
registerLocaleData(localeEn, 'en-BE');
registerLocaleData(localeNl, 'nl-BE');
registerLocaleData(localeFr, 'fr-BE');
registerLocaleData(localeDe, 'de-BE');

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
    {
      provide: LOCALE_ID,
      useFactory: () => {
        const injector = inject(Injector);
        const translate = injector.get(TranslateService);
        return (
          `${translate.getCurrentLang()}-BE` ||
          `${localStorage.getItem('app-language')}-BE` ||
          'en-BE'
        );
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
  ],
};
