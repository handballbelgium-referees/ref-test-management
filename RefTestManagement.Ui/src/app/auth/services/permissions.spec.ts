import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { defer, of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Auth } from './auth';
import { PermissionsService } from './permissions';

describe('PermissionsService', () => {
  let getPermissions: ReturnType<typeof vi.fn>;
  let permissionRequests: number;

  beforeEach(() => {
    vi.useFakeTimers();
    permissionRequests = 0;
    getPermissions = vi.fn().mockReturnValue(
      defer(() => {
        permissionRequests += 1;
        return of(permissionRequests === 1 ? ['ref-tests:view-list'] : []);
      }),
    );
    TestBed.configureTestingModule({
      providers: [
        PermissionsService,
        { provide: HttpClient, useValue: { get: getPermissions } },
        { provide: Auth, useValue: { isAuthenticated$: of(true) } },
      ],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  it('refreshes permissions every minute while authenticated', async () => {
    const service = TestBed.inject(PermissionsService);

    await vi.advanceTimersByTimeAsync(0);
    expect(getPermissions).toHaveBeenCalledTimes(1);
    expect(permissionRequests).toBe(1);
    expect(service.permissions()).toEqual(['ref-tests:view-list']);

    await vi.advanceTimersByTimeAsync(60_000);
    expect(permissionRequests).toBe(2);
    expect(service.permissions()).toEqual([]);
  });

  it('clears permissions when a refresh request fails', async () => {
    getPermissions.mockReturnValue(
      defer(() => {
        permissionRequests += 1;
        return permissionRequests === 1
          ? of(['ref-tests:view-list'])
          : throwError(() => new Error('unavailable'));
      }),
    );
    const service = TestBed.inject(PermissionsService);

    await vi.advanceTimersByTimeAsync(0);
    expect(service.permissions()).toEqual(['ref-tests:view-list']);

    await vi.advanceTimersByTimeAsync(60_000);
    expect(service.permissions()).toEqual([]);
  });
});
