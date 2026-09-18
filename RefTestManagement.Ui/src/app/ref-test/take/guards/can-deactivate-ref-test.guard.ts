import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { RefTestStore } from '../state/ref-test.store';
import { TakeRefTest } from '../take-ref-test';

/**
 * Asks the participant to confirm before they navigate away from an assessment in progress.
 *
 * The confirmation is delegated to the component, which resolves it through the app's own
 * translated dialog. This guard previously called the browser's `confirm()` with an English
 * string, which meant Dutch, French and German participants were interrupted mid-assessment by a
 * message they might not read — and it made `LeaveRefTestDialog` unreachable, because nothing else
 * ever opened it.
 */
export const refTestGuard: CanDeactivateFn<TakeRefTest> = (component) => {
  const store = inject(RefTestStore);
  if (store.completed() || store.withdrawn()) return true;
  return component.canDeactivate();
};
