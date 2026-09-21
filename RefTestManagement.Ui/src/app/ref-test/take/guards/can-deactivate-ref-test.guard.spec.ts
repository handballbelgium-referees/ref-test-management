import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
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
   * hand back whatever it returns, promise included.
   */
  it('hands back a pending confirmation rather than resolving it itself', async () => {
    const pending = Promise.resolve(true);
    expect(await run({ canDeactivate: () => pending })).toBe(true);
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
   * Withdrawing consent erases the attempt on purpose. Asking "are you sure you want to lose your
   * progress?" immediately afterwards would be both confusing and, if confirmed away, would strand
   * the participant on a record that no longer exists.
   */
  it('lets a participant who withdrew consent leave without a prompt', () => {
    const canDeactivate = vi.fn();
    store.withdrawn.set(true);

    expect(run({ canDeactivate })).toBe(true);
    expect(canDeactivate).not.toHaveBeenCalled();
  });
});
