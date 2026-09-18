import { isDevMode, Service } from '@angular/core';

/**
 * The single place application errors are observed.
 *
 * Errors raised in this app routinely carry participant data — a failed mutation echoes the
 * variables it was called with, and a GraphQL error message can name the person it could not find.
 * The browser console is not a private sink: on a shared or supervised machine it is readable by
 * whoever is sitting there, and participants take these tests on machines the organisation does not
 * control. So the full error is only ever printed in development.
 *
 * Production is not silent, though. An app that reports nothing when it breaks cannot be supported,
 * and "it just stopped working" is the least actionable bug report there is. What production logs
 * instead is the *shape* of the failure — where it happened and what class of error it was — both
 * of which are written by us and can never contain participant data.
 */
@Service()
export class ErrorReporter {
  /**
   * @param context Developer-authored label for where the failure surfaced, e.g. an operation name.
   *   Must never be built from user or participant data.
   */
  report(context: string, error: unknown): void {
    if (isDevMode()) {
      console.error(`[${context}]`, error);
      return;
    }

    console.error(`[${context}] ${this.describe(error)}`);
  }

  /** Names the error without revealing anything it carries. */
  private describe(error: unknown): string {
    if (error instanceof Error) {
      return error.name || error.constructor.name;
    }

    if (error && typeof error === 'object') {
      return error.constructor?.name ?? 'Object';
    }

    return typeof error;
  }
}
