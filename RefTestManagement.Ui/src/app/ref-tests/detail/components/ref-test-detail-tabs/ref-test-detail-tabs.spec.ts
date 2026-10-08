import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PermissionsService } from '../../../../auth/services/permissions';
import { RefTestDetailTabs } from './ref-test-detail-tabs';

describe('RefTestDetailTabs', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [RefTestDetailTabs],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        { provide: PermissionsService, useValue: { hasPermission: () => true } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('renders both tabs with neutral text on a white background', async () => {
    const fixture = TestBed.createComponent(RefTestDetailTabs);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const nav = fixture.nativeElement.querySelector('nav') as HTMLElement;
    const tabs = Array.from(nav.querySelectorAll('a'));

    expect(nav.closest('.bg-white')).not.toBeNull();
    expect(tabs).toHaveLength(2);
    for (const tab of tabs) {
      expect(tab.classList).toContain('text-neutral-500');
    }
  });
});
