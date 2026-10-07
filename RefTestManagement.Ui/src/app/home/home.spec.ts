import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { signal } from '@angular/core';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Auth } from '../auth/services/auth';
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
