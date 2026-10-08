import { NO_ERRORS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormField } from '@angular/forms/signals';
import { RouterLink, provideRouter } from '@angular/router';
import { TranslatePipe, provideTranslateService, TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CreateRefTestsGQL, GetQuestionsByNumberGQL } from '../../../../graphql/generated';
import { PermissionsService } from '../../auth/services/permissions';
import { Banner } from '../../services/banner';
import { CreateRefTests } from './create-ref-tests';

describe('CreateRefTests accessible checkbox label', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [CreateRefTests],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        { provide: CreateRefTestsGQL, useValue: { mutate: vi.fn(() => new Subject().asObservable()) } },
        { provide: GetQuestionsByNumberGQL, useValue: { fetch: vi.fn() } },
        { provide: PermissionsService, useValue: { hasPermission: () => true } },
        { provide: Banner, useValue: { error: vi.fn(), success: vi.fn() } },
      ],
    }).overrideComponent(CreateRefTests, {
      set: {
        imports: [FormField, RouterLink, TranslatePipe],
        schemas: [NO_ERRORS_SCHEMA],
      },
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        ref_tests: {
          create: {
            form: {
              random_questions_for_each_user: { label: 'Random questions for each referee' },
            },
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => translate.use('en').subscribe(() => resolve()));
  });

  afterEach(() => TestBed.resetTestingModule());

  it('uses the visible translated label as the checkbox accessible name', async () => {
    const fixture = TestBed.createComponent(CreateRefTests);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const checkbox = fixture.nativeElement.querySelector(
      '#randomQuestionsForEachUser',
    ) as HTMLInputElement;
    const visibleLabel = fixture.nativeElement.querySelector(
      'label[for="randomQuestionsForEachUser"] > div',
    ) as HTMLElement;

    expect(checkbox.getAttribute('aria-label')).toBe('Random questions for each referee');
    expect(checkbox.getAttribute('aria-label')).toBe(visibleLabel.textContent?.trim());
  });
});
