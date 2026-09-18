import { ErrorHandler, inject, Service } from '@angular/core';
import { ErrorReporter } from './error-reporter';

@Service({ autoProvided: false })
export class GlobalErrorHandler implements ErrorHandler {
  private readonly _reporter = inject(ErrorReporter);

  handleError(error: Error): void {
    // Filter out the ResizeObserver error which is harmless but noisy
    if (
      error.message?.includes('ResizeObserver loop') ||
      ((error as any).cause?.type === 'error' &&
        (error as any).cause?.message?.includes('ResizeObserver loop'))
    ) {
      // Silently ignore this error as it's a browser quirk
      return;
    }

    // Log other errors to the console
    this._reporter.report('unhandled', error);
  }
}
