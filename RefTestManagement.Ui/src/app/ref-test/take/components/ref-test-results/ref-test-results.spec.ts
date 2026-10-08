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
            passed: 'Passed',
            failed: 'Failed',
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

  it('renders secondary score text with the higher-contrast token on its white card', () => {
    const fixture = createComponent();
    const nativeElement = fixture.nativeElement as HTMLElement;
    const total = nativeElement.querySelector('.text-lg.text-neutral-500') as HTMLElement | null;
    expect(total).not.toBeNull();
    const scoreCard = total?.closest('.bg-white');
    expect(scoreCard).not.toBeNull();

    const secondaryText = Array.from(scoreCard?.querySelectorAll('.text-neutral-500') ?? []);

    expect(secondaryText).toHaveLength(5);
    expect(scoreCard?.classList).toContain('bg-white');
    for (const element of secondaryText) {
      expect(element.classList).toContain('text-neutral-500');
    }
  });

  it('keeps the result indeterminate when the passing threshold is unavailable or invalid', () => {
    for (const passingPercentage of [null, -1, 101, Number.NaN]) {
      const fixture = createComponent({ passingPercentage });
      expect(fixture.nativeElement.querySelector('.w-20')).toBeNull();
      expect(
        (fixture.nativeElement.querySelector('.text-5xl') as HTMLElement).classList,
      ).toContain('text-neutral-900');
    }
  });

  it('uses a configured zero passing threshold normally', () => {
    const fixture = createComponent({ percentage: 0, passingPercentage: 0 });

    expect(fixture.nativeElement.querySelector('.w-20')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.w-20').classList).toContain('bg-success-100');
    expect(fixture.nativeElement.textContent).toContain('Passed');
  });

  it('renders an explicit failed result when the score is below the threshold', () => {
    const fixture = createComponent({ percentage: 79, passingPercentage: 80 });

    expect(fixture.nativeElement.textContent).toContain('Failed');
  });
});
