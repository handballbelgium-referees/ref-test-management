import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RefTestStore } from '../state/ref-test.store';
import { TakeRefTest } from '../take-ref-test';
import { refTestGuard } from './can-deactivate-ref-test.guard';

/**
 * Covers the gate that stands between a participant and losing an assessment in progress.
 *
 * Two things can go wrong and neither throws. Prompting when there is nothing to lose trains
 * participants to dismiss the dialog, so the one time it matters they click through it. Failing to
 * prompt when there *is* something to lose discards the attempt silently.
 *
 * The guard also has to delegate rather than decide: it previously called the browser's `confirm()`
 * with a hardcoded English string, which interrupted Dutch, French and German participants
 * mid-assessment with a message they might not read.
 */
describe('refTestGuard', () => {
  let store: RefTestStore;

  const run = (component: Partial<TakeRefTest>) =>
    TestBed.runInInjectionContext(() =>
      refTestGuard(
        component as TakeRefTest,
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
        {} as RouterStateSnapshot,
      ),
    );

  const takingComponent = (flushPendingSave: () => Observable<boolean>) => {
    const component = Object.create(TakeRefTest.prototype) as TakeRefTest;
    Object.assign(component, {
      store: {
        completed: () => false,
        showLeaveDialog: signal(false),
      } as unknown as RefTestStore,
      _facade: { flushPendingSave },
      _leaveConfirmed: false,
      _tempLeaveHandlers: undefined,
      leaveSavePending: signal(false),
      leaveSaveFailed: signal(false),
    });
    return component;
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        RefTestStore,
        { provide: TranslateService, useValue: { currentLang: () => 'en' } },
      ],
    });
    store = TestBed.inject(RefTestStore);
  });

  it('asks the component when an assessment is still in progress', () => {
    const canDeactivate = vi.fn().mockReturnValue(false);

    expect(run({ canDeactivate })).toBe(false);
    expect(canDeactivate).toHaveBeenCalledOnce();
  });

  it('passes the component’s answer straight through', () => {
    expect(run({ canDeactivate: () => true })).toBe(true);
  });

  /**
   * The component resolves the prompt through the app's own translated dialog, so the guard must
   * hand back whatever it returns, observable included.
   */
  it('hands back a pending confirmation rather than resolving it itself', () => {
    const pending = new Subject<boolean>();
    expect(run({ canDeactivate: () => pending })).toBe(pending);
  });

  it('waits for the confirmed progress flush before allowing navigation', () => {
    const savePending = new Subject<boolean>();
    const component = takingComponent(() => savePending);
    const answers: boolean[] = [];
    let completed = false;
    (run(component) as Observable<boolean>).subscribe({
      next: (answer) => answers.push(answer),
      complete: () => (completed = true),
    });
    component.confirmLeave();

    expect(answers).toEqual([]);
    expect(component.leaveSavePending()).toBe(true);

    savePending.next(true);
    expect(answers).toEqual([true]);
    expect(completed).toBe(true);
    expect(component.leaveSavePending()).toBe(false);
  });

  it('keeps the participant on the test when the confirmed progress flush fails', () => {
    const component = takingComponent(() => of(false));
    let answer: boolean | undefined;
    (run(component) as Observable<boolean>).subscribe((result) => (answer = result));
    component.confirmLeave();

    expect(answer).toBe(false);
    expect(component.leaveSaveFailed()).toBe(true);
  });

  /**
   * After submitting there is nothing left to lose, and the result page is reached by navigating
   * away. Prompting here would make the participant confirm leaving a finished assessment.
   */
  it('lets a finished participant leave without a prompt', () => {
    const canDeactivate = vi.fn();
    store.completed.set(true);

    expect(run({ canDeactivate })).toBe(true);
    expect(canDeactivate).not.toHaveBeenCalled();
  });

  /**
   * Once a withdrawal request is durably queued, the page is replaced by its acknowledgement.
   * Asking whether to leave the attempt would imply that the queued request could be undone.
   */
  it('lets a participant with a queued withdrawal leave without a prompt', () => {
    const canDeactivate = vi.fn();
    store.withdrawalQueued.set(true);

    expect(run({ canDeactivate })).toBe(true);
    expect(canDeactivate).not.toHaveBeenCalled();
  });
});
