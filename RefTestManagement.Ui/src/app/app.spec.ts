import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Auth } from './auth/services/auth';
import { App } from './app';
import { LanguageConfig } from './services/language-config';
import { PwaUpdate } from './services/pwa-update';

describe('App language selector', () => {
  let fixture: ComponentFixture<App>;
  let translate: TranslateService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: Auth,
          useValue: {
            isAuthenticated: () => false,
            user: () => null,
            login: vi.fn(),
            logout: vi.fn(),
          },
        },
        {
          provide: LanguageConfig,
          useValue: {
            initialLanguage: 'en',
            getAvailableLanguages: () =>
              of([
                { code: 'en', name: 'English' },
                { code: 'nl', name: 'Nederlands' },
              ]),
          },
        },
        { provide: PwaUpdate, useValue: { initializeUpdateCheck: vi.fn() } },
      ],
    });

    translate = TestBed.inject(TranslateService);
    translate.addLangs(['en', 'nl']);
    translate.setTranslation(
      'en',
      {
        app: { title: 'RefTest', page_title: 'RefTest' },
        nav: {
          language: 'Language',
          select_language: 'Switch to {{language}}',
          logout: 'Logout',
        },
        footer: {
          copyright: 'Copyright {{year}}',
          creator: 'Created by the team',
          privacy: 'Privacy',
        },
      },
      true,
    );
    translate.setTranslation('nl', {}, true);
    translate.use('en').subscribe();

    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('uses native disclosure buttons and restores focus after Escape or selection', async () => {
    const toggle = fixture.nativeElement.querySelector(
      'button[aria-label="Language"]',
    ) as HTMLButtonElement;
    toggle.click();
    fixture.detectChanges();

    const options = fixture.nativeElement.querySelector('#language-options') as HTMLElement;
    const languageButtons = Array.from(
      options.querySelectorAll<HTMLButtonElement>('button'),
    );
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(toggle.getAttribute('aria-controls')).toBe('language-options');
    expect(options.getAttribute('role')).toBeNull();
    expect(languageButtons[0]?.getAttribute('role')).toBeNull();
    expect(languageButtons[0]?.getAttribute('aria-label')).toBe('Switch to English');

    languageButtons[0]?.focus();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#language-options')).toBeNull();
    expect(document.activeElement).toBe(toggle);

    toggle.click();
    fixture.detectChanges();
    const dutch = fixture.nativeElement.querySelector(
      'button[aria-label="Switch to Nederlands"]',
    ) as HTMLButtonElement;
    dutch.click();
    fixture.detectChanges();

    await vi.waitFor(() => expect(translate.getCurrentLang()).toBe('nl'));
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(toggle);
  });
});
