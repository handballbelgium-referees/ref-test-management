import { CanDeactivateFn } from '@angular/router';
import { TakeQuizComponent } from '../take-quiz';

export const canDeactivateQuizGuard: CanDeactivateFn<TakeQuizComponent> = (component) => {
  return component.canDeactivate();
};
