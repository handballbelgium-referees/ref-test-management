import { ErrorHandler, Injectable } from '@angular/core';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
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
    console.error('An error occurred:', error);
  }
}
