import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { PermissionsService } from '../../../../../../auth/services/permissions';
import { TestInfoCard } from './test-info-card';

describe('TestInfoCard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TestInfoCard],
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        { provide: PermissionsService, useValue: { hasPermission: () => true } },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('renders neutral-500 placeholders on its neutral-50 background', () => {
    const fixture = TestBed.createComponent(TestInfoCard);
    fixture.componentRef.setInput('numberOfQuestions', 10);
    fixture.componentRef.setInput('status', 'PENDING');
    fixture.componentRef.setInput('isAnonymized', true);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('.bg-neutral-50') as HTMLElement;
    const placeholders = Array.from(card.querySelectorAll('dd span'));

    expect(placeholders).toHaveLength(2);
    for (const placeholder of placeholders) {
      expect(placeholder.classList).toContain('text-neutral-500');
    }
  });
});
