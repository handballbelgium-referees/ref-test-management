import { TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { RefTestResults } from './ref-test-results';

describe('RefTestResults', () => {
  const questions = [
    {
      id: 'q1',
      number: '1',
      phrase: { en: 'Question 1' },
      answers: [
        { id: 'a1', number: 'A', phrase: { en: 'Correct answer' }, isCorrect: true },
        { id: 'a2', number: 'B', phrase: { en: 'Wrong answer' }, isCorrect: false },
      ],
    },
  ];

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        ref_test: {
          results: {
            title: 'Results',
            subtitle: 'Subtitle',
            percentage: 'Percentage',
            question_score: 'Question score',
            answer_score: 'Answer score',
            email_info_shortly: 'Mail will be sent shortly',
            email_info: 'Mail will be sent in {{minutes}} minutes',
          },
        },
        ref_tests: {
          detail: {
            correct: 'Correct',
            incorrect: 'Incorrect',
            answers_summary: 'Answers summary',
            answers_selected: '{{selected}} of {{total}} answered',
          },
        },
      },
      true,
    );

    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  function createComponent(inputs?: Partial<Record<string, unknown>>) {
    const fixture = TestBed.createComponent(RefTestResults);
    fixture.componentRef.setInput('questions', questions);
    fixture.componentRef.setInput('questionScore', 1);
    fixture.componentRef.setInput('questionTotal', 1);
    fixture.componentRef.setInput('answerScore', 1);
    fixture.componentRef.setInput('answerTotal', 2);
    fixture.componentRef.setInput('percentage', 100);
    fixture.componentRef.setInput('passingPercentage', 80);
    fixture.componentRef.setInput('emailDelayMinutes', 5);
    fixture.componentRef.setInput('currentLanguage', 'en');
    fixture.componentRef.setInput('selectedAnswerIds', ['a1']);
    fixture.componentRef.setInput('sendResultsAutomatically', true);
    fixture.componentRef.setInput('resultsSent', false);

    for (const [key, value] of Object.entries(inputs ?? {})) {
      fixture.componentRef.setInput(key, value);
    }

    fixture.detectChanges();
    return fixture;
  }

  it('shows the pending email banner only while the results mail has not been sent yet', () => {
    const pendingFixture = createComponent({ resultsSent: false });
    expect(pendingFixture.nativeElement.textContent).toContain('Mail will be sent in 5 minutes');

    const sentFixture = createComponent({ resultsSent: true });
    expect(sentFixture.nativeElement.textContent).not.toContain('Mail will be sent in 5 minutes');
  });

  it('shows the answer review only after the results mail has been sent', () => {
    const pendingFixture = createComponent({ resultsSent: false });
    expect(pendingFixture.nativeElement.textContent).not.toContain('Question 1');
    expect(pendingFixture.nativeElement.textContent).not.toContain('Answers summary');

    const sentFixture = createComponent({ resultsSent: true });
    const text = sentFixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();

    expect(text).toContain('Question 1');
    expect(text).toContain('Correct answer');
    expect(text).toContain('Correct');
    expect(text).toContain('Answers summary');
    expect(text).toContain('1 of 1 answered');
  });
});
