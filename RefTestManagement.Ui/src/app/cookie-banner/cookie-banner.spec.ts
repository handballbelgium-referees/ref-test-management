import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import en from '../../../public/i18n/en.json';
import { COOKIE_NOTICE_STORAGE_KEY, CookieBanner } from './cookie-banner';

describe('CookieBanner', () => {
  beforeEach(async () => {
    localStorage.removeItem(COOKIE_NOTICE_STORAGE_KEY);
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ fallbackLang: 'en' }), provideRouter([])],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en, true);
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    localStorage.removeItem(COOKIE_NOTICE_STORAGE_KEY);
  });

  function render(): ComponentFixture<CookieBanner> {
    const fixture = TestBed.createComponent(CookieBanner);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the banner with a link to the cookie policy when not yet acknowledged', () => {
    const fixture = render();
    const region = fixture.nativeElement.querySelector('[role="region"]') as HTMLElement | null;

    expect(region).not.toBeNull();
    expect(region?.textContent).toContain(en.cookieBanner.message);
    const link = region?.querySelector('a');
    expect(link?.getAttribute('href')).toBe('/cookies');
  });

  it('hides the banner and stores the acknowledgement when accepted', () => {
    const fixture = render();
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="region"]')).toBeNull();
    expect(localStorage.getItem(COOKIE_NOTICE_STORAGE_KEY)).toBe('true');
  });

  it('stays hidden when the notice was already acknowledged', () => {
    localStorage.setItem(COOKIE_NOTICE_STORAGE_KEY, 'true');
    const fixture = render();

    expect(fixture.nativeElement.querySelector('[role="region"]')).toBeNull();
  });
});
