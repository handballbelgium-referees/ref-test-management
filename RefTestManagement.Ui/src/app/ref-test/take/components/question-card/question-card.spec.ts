import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { QuestionCard } from './question-card';

describe('QuestionCard', () => {
  let fixture: ComponentFixture<QuestionCard>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [QuestionCard],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', { ref_test: { question: 'Question' } }, true);
    await new Promise<void>((resolve) => translate.use('en').subscribe(() => resolve()));

    fixture = TestBed.createComponent(QuestionCard);
    fixture.componentRef.setInput('question', {
      id: 'q1',
      phrase: { en: 'First question' },
      answers: [],
    });
    fixture.componentRef.setInput('currentIndex', 0);
    fixture.componentRef.setInput('totalQuestions', 2);
    fixture.componentRef.setInput('currentLanguage', 'en');
    fixture.componentRef.setInput('selectedAnswerIds', []);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('announces the active question number and text when the question changes', async () => {
    fixture.componentRef.setInput('currentIndex', 1);
    fixture.componentRef.setInput('question', {
      id: 'q2',
      phrase: { en: 'Second question' },
      answers: [],
    });
    fixture.detectChanges();
    await fixture.whenStable();

    const liveRegion = fixture.nativeElement.querySelector(
      '[aria-live="polite"][aria-atomic="true"]',
    ) as HTMLElement | null;
    expect(liveRegion?.textContent).toContain('Question 2 / 2');
    expect(liveRegion?.textContent).toContain('Second question');
  });
});
