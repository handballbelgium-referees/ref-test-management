import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { signal } from '@angular/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Auth } from '../auth/services/auth';
import { Permissions } from '../auth/models/permissions';
import { PermissionsService } from '../auth/services/permissions';
import { Banner as BannerService } from '../services/banner';
import { Home } from './home';

type AuthenticationState =
  | { status: 'checking' }
  | { status: 'authenticated' }
  | { status: 'unauthenticated' }
  | { status: 'error' };

describe('Home', () => {
  let fixture: ComponentFixture<Home>;
  let authenticationState: ReturnType<typeof signal<AuthenticationState>>;
  let retryAuthenticationCheck: ReturnType<typeof vi.fn>;
  let login: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    authenticationState = signal<AuthenticationState>({ status: 'checking' });
    retryAuthenticationCheck = vi.fn();
    login = vi.fn();
    TestBed.configureTestingModule({
      imports: [Home],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: Auth,
          useValue: { authenticationState, retryAuthenticationCheck, login },
        },
        {
          provide: PermissionsService,
          useValue: {
            permissions: signal(['superadmin']),
            hasPermission: vi.fn(() => true),
          },
        },
        {
          provide: BannerService,
          useValue: {
            banners: signal([]),
            dismiss: vi.fn(),
            executeAction: vi.fn(),
          },
        },
      ],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        common: { retry: 'Retry' },
        home: {
          hero: { login: 'Login' },
          authenticationCheckFailed: {
            title: 'Sign-in status unavailable',
            description: 'Retry the check to continue.',
          },
          actions: { title: 'Quick Actions' },
        },
        ref_tests: { list: { loading_more: 'Loading...' } },
      },
      true,
    );
    await new Promise<void>((resolve) => translate.use('en').subscribe(() => resolve()));
    fixture = TestBed.createComponent(Home);
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  function button(label: string): HTMLButtonElement | undefined {
    const nativeElement = fixture.nativeElement as HTMLElement;
    return Array.from(nativeElement.querySelectorAll<HTMLButtonElement>('button')).find(
      (element) => element.textContent?.trim() === label,
    );
  }

  it('shows authenticated quick actions after a successful check', () => {
    authenticationState.set({ status: 'authenticated' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('a[href="/ref-tests/create"]')).not.toBeNull();
    expect(button('Login')).toBeUndefined();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('shows privacy withdrawal review only for its dedicated permission', () => {
    vi.spyOn(TestBed.inject(PermissionsService), 'hasPermission').mockImplementation(
      (permission) => permission === Permissions.PrivacyOperations.ReviewWithdrawals,
    );
    authenticationState.set({ status: 'authenticated' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('a[href="/privacy/withdrawal-review"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/ref-tests/create"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/ref-tests"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/audit-logs"]')).toBeNull();
  });

  it('announces authentication loading through a status region', () => {
    authenticationState.set({ status: 'checking' });
    fixture.detectChanges();

    const status = fixture.nativeElement.querySelector('[role="status"]') as HTMLElement | null;
    const spinner = status?.querySelector('.animate-spin');

    expect(status?.getAttribute('aria-atomic')).toBe('true');
    expect(status?.textContent?.trim()).toBe('Loading...');
    expect(spinner?.getAttribute('aria-hidden')).toBe('true');
  });

  it('keeps the manage-card description readable across its gradient', () => {
    authenticationState.set({ status: 'authenticated' });
    fixture.detectChanges();

    const nativeElement = fixture.nativeElement as HTMLElement;
    const card = nativeElement.querySelector('a[href="/ref-tests"]') as HTMLAnchorElement | null;
    const description = card?.querySelector('p');
    expect(card).not.toBeNull();
    expect(description).not.toBeNull();
    expect(card?.classList).toContain('from-secondary-700');
    expect(card?.classList).toContain('to-secondary-800');
    expect(card?.classList).toContain('text-white');

    expect(description?.classList).not.toContain('text-secondary-100');
  });

  it('shows login only after the check confirms the user is unauthenticated', () => {
    authenticationState.set({ status: 'unauthenticated' });
    fixture.detectChanges();

    button('Login')?.click();

    expect(login).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('offers retry on failure and recovers without showing a misleading login action', () => {
    authenticationState.set({ status: 'error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain(
      'Sign-in status unavailable',
    );
    expect(button('Login')).toBeUndefined();

    button('Retry')?.click();
    expect(retryAuthenticationCheck).toHaveBeenCalledOnce();

    authenticationState.set({ status: 'checking' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.animate-spin')).not.toBeNull();
    expect(button('Login')).toBeUndefined();

    authenticationState.set({ status: 'authenticated' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('a[href="/ref-tests/create"]')).not.toBeNull();
    expect(button('Login')).toBeUndefined();
  });
});
