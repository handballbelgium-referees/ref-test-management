import { CanDeactivateFn } from '@angular/router';
import { TakeRefTest } from '../take-ref-test';

export const canDeactivateRefTestGuard: CanDeactivateFn<TakeRefTest> = (component) => {
  return component.canDeactivate();
};
