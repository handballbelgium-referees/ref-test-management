import { Injector, runInInjectionContext, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createDialogOperation } from './dialog-utils';

describe('createDialogOperation', () => {
  afterEach(() => TestBed.resetTestingModule());

  it('does not invoke a confirmation callback more than once while it is in flight', () => {
    TestBed.configureTestingModule({});
    const callback = vi.fn(() => ({
      loading: signal(false).asReadonly(),
      success: signal(false).asReadonly(),
      error: signal<unknown>(null).asReadonly(),
    }));
    const operation = runInInjectionContext(TestBed.inject(Injector), () =>
      createDialogOperation(callback, () => ['selected-ref-test']),
    );

    operation.confirm(undefined);
    operation.confirm(undefined);

    expect(callback).toHaveBeenCalledTimes(1);
  });
});
