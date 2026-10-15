import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { beforeEach, describe, expect, it } from 'vitest';
import en from '../../../public/i18n/en.json';
import { CookiePolicy } from './cookie-policy';

describe('CookiePolicy', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ fallbackLang: 'en' }), provideRouter([])],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en, true);
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  it('renders the cookie policy title and every cookie section', () => {
    const fixture = TestBed.createComponent(CookiePolicy);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent.replace(/\s+/g, ' ');

    expect(text).toContain(en.cookies.title);
    expect(text).toContain(en.cookies.intro);
    expect(text).toContain(en.cookies.authBody);
    expect(text).toContain(en.cookies.loginBody);
    expect(text).toContain(en.cookies.storageBody);
    expect(text).toContain(en.cookies.thirdPartyBody);
  });
});
