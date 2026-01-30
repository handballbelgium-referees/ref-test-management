import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { RefTestStore } from '../state/ref-test.store';
import { TakeRefTest } from '../take-ref-test';

export const refTestGuard: CanDeactivateFn<TakeRefTest> = () => {
  const store = inject(RefTestStore);
  if (store.completed()) return true;
  return confirm('Are you sure you want to leave the test?');
};
