import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Question } from './ref-test.models';
import { RefTestStore } from './ref-test.store';

/**
 * Covers the participant-facing state the assessment runs on.
 *
 * This is the one flow in the product with no server-side safety net during the attempt: the
 * countdown, the answer set and the navigation gate all live in the browser until the participant
 * submits. A defect here does not throw — it quietly gives one participant more time than another,
 * or lets them read ahead, or loses an answer. So the assertions below are about the rules of the
 * assessment rather than about the mechanics of signals.
 */
describe('RefTestStore', () => {
  const question = (id: string, ...answerIds: string[]): Question => ({
    id,
    phrase: { en: id },
    answers: answerIds.map((answerId) => ({ id: answerId, phrase: { en: answerId } })),
  });

  const questions = [
    question('q1', 'q1a1', 'q1a2'),
    question('q2', 'q2a1', 'q2a2'),
    question('q3', 'q3a1', 'q3a2'),
  ];

  let store: RefTestStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        RefTestStore,
        { provide: TranslateService, useValue: { currentLang: () => 'en' } },
      ],
    });
    store = TestBed.inject(RefTestStore);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('the countdown', () => {
    /**
     * The remaining time is derived from the start timestamp rather than decremented on each tick.
     * A decrementing counter drifts whenever the tab is throttled or the machine sleeps, which on
     * a timed assessment means a participant who backgrounds the tab is handed extra minutes.
     */
    it('derives the time left from the start time rather than counting ticks', () => {
      vi.useFakeTimers();
      const start = new Date('2026-01-01T10:00:00Z');
      vi.setSystemTime(start);

      store.setTimer(start, 30);
      expect(store.timeRemainingSeconds()).toBe(30 * 60);

      // Nothing ticks here. Only wall-clock time passes, as it would in a throttled tab.
      vi.setSystemTime(new Date('2026-01-01T10:10:00Z'));
      store.updateRemainingTime();

      expect(store.timeRemainingSeconds()).toBe(20 * 60);
    });

    /**
     * Auto-submit fires on exactly zero. If an overrun could produce a negative value the equality
     * check in the component's tick effect would never match and the assessment would run forever.
     */
    it('clamps at zero so a late tick still triggers auto-submit', () => {
      vi.useFakeTimers();
      const start = new Date('2026-01-01T10:00:00Z');
      vi.setSystemTime(start);
      store.setTimer(start, 10);

      vi.setSystemTime(new Date('2026-01-01T10:45:00Z'));
      store.updateRemainingTime();

      expect(store.timeRemainingSeconds()).toBe(0);
    });

    it('leaves the clock alone until the assessment has actually started', () => {
      store.updateRemainingTime();
      expect(store.timeRemainingSeconds()).toBe(0);
    });

    /**
     * Extending time is an invigilator action taken while the participant is mid-assessment, so it
     * has to take effect against the elapsed time rather than restart the clock.
     */
    it('credits extra time against the time already elapsed', () => {
      vi.useFakeTimers();
      const start = new Date('2026-01-01T10:00:00Z');
      vi.setSystemTime(start);
      store.setTimer(start, 10);

      vi.setSystemTime(new Date('2026-01-01T10:09:00Z'));
      store.extendTime(20);

      expect(store.timeRemainingSeconds()).toBe(11 * 60);
    });

    it.each([
      [90, '1:30'],
      [59, '0:59'],
      [5, '0:05'],
      [0, '0:00'],
      [3600, '60:00'],
    ])('renders %i seconds as %s', (seconds, expected) => {
      store.timeRemainingSeconds.set(seconds);
      expect(store.formattedTimeRemaining()).toBe(expected);
    });
  });

  describe('navigation', () => {
    beforeEach(() => {
      store.initQuestions(questions);
      store.visitedQuestions.set(new Set(['q1']));
    });

    /**
     * Participants may revisit what they have already seen but must not skip ahead — jumping to an
     * unvisited question would let them read the whole paper before answering any of it.
     */
    it('refuses a jump to a question the participant has not reached yet', () => {
      store.goToQuestion(2);
      expect(store.currentQuestionIndex()).toBe(0);
    });

    it('allows a jump back to a question already visited', () => {
      store.nextQuestion();
      store.goToQuestion(0);
      expect(store.currentQuestionIndex()).toBe(0);
    });

    it('marks each question visited as it is reached', () => {
      store.nextQuestion();
      expect(store.visitedQuestions().has('q2')).toBe(true);
      expect(store.currentQuestionIndex()).toBe(1);
    });

    it('does not run past the last question', () => {
      store.nextQuestion();
      store.nextQuestion();
      store.nextQuestion();
      expect(store.currentQuestionIndex()).toBe(2);
      expect(store.isLastQuestion()).toBe(true);
      expect(store.canNext()).toBe(false);
    });

    it('does not run before the first question', () => {
      store.previousQuestion();
      expect(store.currentQuestionIndex()).toBe(0);
      expect(store.canPrevious()).toBe(false);
    });
  });

  describe('answers', () => {
    beforeEach(() => store.initQuestions(questions));

    it('toggles a selection off when it is chosen twice', () => {
      store.selectAnswer('q1', 'q1a1');
      expect([...store.selectedAnswers()['q1']]).toEqual(['q1a1']);

      store.selectAnswer('q1', 'q1a1');
      expect(store.selectedAnswers()['q1'].size).toBe(0);
    });

    it('keeps multiple selections on one question', () => {
      store.selectAnswer('q1', 'q1a1');
      store.selectAnswer('q1', 'q1a2');
      expect(store.selectedAnswers()['q1'].size).toBe(2);
    });

    /**
     * The signal holds Sets, which are mutable. Updating in place would leave the reference
     * unchanged and the view would not repaint the selection the participant just made.
     */
    it('replaces the answer map rather than mutating it', () => {
      const before = store.selectedAnswers();
      store.selectAnswer('q1', 'q1a1');
      expect(store.selectedAnswers()).not.toBe(before);
      expect(before['q1'].size).toBe(0);
    });

    it('counts only questions that actually have an answer', () => {
      store.selectAnswer('q1', 'q1a1');
      store.selectAnswer('q2', 'q2a1');
      store.selectAnswer('q2', 'q2a1');
      expect(store.answeredCount()).toBe(1);
    });

    it('flattens every selection for submission', () => {
      store.selectAnswer('q1', 'q1a1');
      store.selectAnswer('q3', 'q3a2');
      expect(store.getSelectedAnswerIds().sort()).toEqual(['q1a1', 'q3a2']);
    });
  });

  describe('restoring progress', () => {
    /**
     * Resuming after a refresh or a dropped connection. Answer ids arrive as a flat list, so the
     * store has to map each one back to the question that owns it; getting this wrong loses the
     * participant's work without any visible error.
     */
    it('maps saved answers back onto the questions that own them', () => {
      store.restoreProgress(['q1a2', 'q2a1'], 1, questions);

      expect([...store.selectedAnswers()['q1']]).toEqual(['q1a2']);
      expect([...store.selectedAnswers()['q2']]).toEqual(['q2a1']);
      expect(store.selectedAnswers()['q3'].size).toBe(0);
      expect(store.currentQuestionIndex()).toBe(1);
    });

    it('treats everything up to the resume point as already seen', () => {
      store.restoreProgress([], 1, questions);

      expect(store.visitedQuestions().has('q1')).toBe(true);
      expect(store.visitedQuestions().has('q2')).toBe(true);
      expect(store.visitedQuestions().has('q3')).toBe(false);
    });

    it('ignores an answer id that belongs to no question', () => {
      store.restoreProgress(['ghost'], 0, questions);
      expect(store.getSelectedAnswerIds()).toEqual([]);
    });

    it('says nothing when there was no progress to restore', () => {
      store.restoreProgress([], 0, questions);
      expect(store.showProgressRestored()).toBe(false);
    });

    it('tells the participant when their work came back', () => {
      store.restoreProgress(['q1a1'], 0, questions);
      expect(store.showProgressRestored()).toBe(true);
    });

    /**
     * Restoring twice in quick succession used to leave the first timer running, so it dismissed
     * the second notice early.
     */
    it('does not let an earlier notice dismiss a later one', () => {
      vi.useFakeTimers();

      store.restoreProgress(['q1a1'], 0, questions);
      vi.advanceTimersByTime(9_000);

      store.restoreProgress(['q1a1'], 0, questions);
      vi.advanceTimersByTime(2_000);

      expect(store.showProgressRestored()).toBe(true);

      vi.advanceTimersByTime(9_000);
      expect(store.showProgressRestored()).toBe(false);
    });
  });

  describe('completion', () => {
    it('starts auto-submit only once until the attempt is reset', () => {
      expect(store.beginAutoSubmit()).toBe(true);
      expect(store.beginAutoSubmit()).toBe(false);

      store.reset();

      expect(store.beginAutoSubmit()).toBe(true);
    });

    it('records the result and closes the confirmation dialog', () => {
      store.showSubmitDialog.set(true);
      store.complete({ percentage: 85.5, sendResultsAutomatically: true });

      expect(store.completed()).toBe(true);
      expect(store.result()?.percentage).toBe(85.5);
      expect(store.sendResultsAutomatically()).toBe(true);
      expect(store.showSubmitDialog()).toBe(false);
    });

    it('defaults to not emailing results when the flag is absent', () => {
      store.complete({ percentage: 40 });
      expect(store.sendResultsAutomatically()).toBe(false);
    });

    it('restores a completed result with the token needed for later participant actions', () => {
      store.restoreCompletedResult('token-123', {
        questionScore: 8,
        questionTotal: 10,
        answerScore: 15,
        answerTotal: 20,
        percentage: 80,
        sendResultsAutomatically: true,
      });

      expect(store.token()).toBe('token-123');
      expect(store.completed()).toBe(true);
      expect(store.result()?.percentage).toBe(80);
      expect(store.sendResultsAutomatically()).toBe(true);
    });

    /**
     * The store outlives a single attempt when a participant returns to the landing page, so a
     * stale flag left behind here would mislabel the next attempt.
     */
    it('clears every trace of the attempt on reset', () => {
      store.initQuestions(questions);
      store.selectAnswer('q1', 'q1a1');
      store.setTimer(new Date(), 30);
      store.complete({ percentage: 90 });
      store.token.set('abc');
      store.withdrawn.set(true);

      store.reset();

      expect(store.questions()).toEqual([]);
      expect(store.getSelectedAnswerIds()).toEqual([]);
      expect(store.completed()).toBe(false);
      expect(store.result()).toBeNull();
      expect(store.withdrawn()).toBe(false);
      expect(store.token()).toBe('');
      expect(store.startTime()).toBeNull();
      expect(store.timeRemainingSeconds()).toBe(0);
    });
  });
});
