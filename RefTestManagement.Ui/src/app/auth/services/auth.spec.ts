import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of, Subject, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Auth } from './auth';

describe('Auth', () => {
  let authenticationResponses: Observable<boolean>[];
  let userResponse: Observable<unknown>;
  let get: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    authenticationResponses = [];
    userResponse = of({ name: 'Referee' });
    get = vi.fn((url: string) =>
      url === '/Account/IsAuthenticated'
        ? (authenticationResponses.shift() ?? of(false))
        : userResponse,
    );
    TestBed.configureTestingModule({
      providers: [Auth, { provide: HttpClient, useValue: { get } }],
    });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('reports a pending check and authenticated success', () => {
    const response = new Subject<boolean>();
    authenticationResponses.push(response);
    const auth = TestBed.inject(Auth);

    expect(auth.authenticationState()).toEqual({ status: 'checking' });
    expect(auth.isAuthenticated()).toBeUndefined();

    response.next(true);
    response.complete();

    expect(auth.authenticationState()).toEqual({ status: 'authenticated' });
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.user()).toEqual({ name: 'Referee' });
  });

  it('distinguishes an unauthenticated response from a failed check', () => {
    authenticationResponses.push(of(false));
    const auth = TestBed.inject(Auth);

    expect(auth.authenticationState()).toEqual({ status: 'unauthenticated' });
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.user()).toBeNull();
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('recovers from a failed check only after an explicit retry', () => {
    const retryResponse = new Subject<boolean>();
    authenticationResponses.push(throwError(() => new Error('unavailable')), retryResponse);
    const auth = TestBed.inject(Auth);

    expect(auth.authenticationState()).toEqual({ status: 'error' });
    expect(auth.isAuthenticated()).toBeUndefined();
    expect(auth.user()).toBeUndefined();
    expect(get).toHaveBeenCalledTimes(1);

    auth.retryAuthenticationCheck();

    expect(auth.authenticationState()).toEqual({ status: 'checking' });
    expect(get).toHaveBeenCalledTimes(2);

    retryResponse.next(true);
    retryResponse.complete();

    expect(auth.authenticationState()).toEqual({ status: 'authenticated' });
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.user()).toEqual({ name: 'Referee' });
  });

  it('keeps user-fetch failures separate from authentication-check failures', () => {
    authenticationResponses.push(of(true));
    userResponse = throwError(() => new Error('user unavailable'));
    const auth = TestBed.inject(Auth);

    expect(auth.authenticationState()).toEqual({ status: 'authenticated' });
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.user()).toBeNull();
  });
});
