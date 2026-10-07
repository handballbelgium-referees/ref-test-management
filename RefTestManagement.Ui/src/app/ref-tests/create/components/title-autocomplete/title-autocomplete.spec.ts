import { registerLocaleData } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GetRefTestTitlesGQL } from '../../../../../../graphql/generated';
import { TitleAutocomplete } from './title-autocomplete';

interface ITitleSearchResult {
  data?: {
    refTestTitles?: {
      edges?: Array<{ node?: { id?: string | null; value?: string | null } | null } | null> | null;
      pageInfo?: { hasNextPage?: boolean; endCursor?: string | null } | null;
    } | null;
  };
  loading: boolean;
  error?: unknown;
}

interface ITitleAutocompleteHarness {
  onFocus(): void;
  onSearchInput(event: Event): void;
  onManualEntry(): void;
  onBlur(): void;
  searchError(): boolean;
  suggestions(): Array<{ id?: string; value: string }>;
}

describe('TitleAutocomplete', () => {
  const results: Subject<ITitleSearchResult>[] = [];
  const exactResults: Subject<ITitleSearchResult>[] = [];
  const watch = vi.fn(() => {
    const result = new Subject<ITitleSearchResult>();
    results.push(result);
    return { valueChanges: result.asObservable() };
  });
  const fetch = vi.fn(() => {
    const result = new Subject<ITitleSearchResult>();
    exactResults.push(result);
    return result.asObservable();
  });

  beforeEach(async () => {
    results.length = 0;
    exactResults.length = 0;
    watch.mockClear();
    fetch.mockClear();
    const englishLocale = await import('@angular/common/locales/en');
    registerLocaleData(englishLocale.default, 'en-BE');
    TestBed.configureTestingModule({
      imports: [TitleAutocomplete],
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        { provide: GetRefTestTitlesGQL, useValue: { watch, fetch } },
      ],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        common: { retry: 'Retry' },
        ref_tests: {
          create: {
            form: {
              title: {
                label: 'RefTest Title',
                placeholder: 'Search for a title',
                tooltip: 'Example: Training {{date}}',
                search_error: 'Could not search titles.',
                no_results: 'No existing titles found',
                create_new: 'Press Enter to create a new title',
              },
            },
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => {
      translate.use('en').subscribe(() => resolve());
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  it('exposes the active listbox option and selects it with Enter', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();

    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    component.onFocus();
    component.onSearchInput({ target: { value: 'Training' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [
            { node: { id: 'title-1', value: 'Training A' } },
            { node: { id: 'title-2', value: 'Training B' } },
          ],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const listbox = fixture.nativeElement.querySelector('[role="listbox"]') as HTMLElement;
    expect(input.getAttribute('role')).toBe('combobox');
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-controls')).toBe(listbox.id);
    expect(listbox.querySelectorAll('[role="option"]')).toHaveLength(2);

    input.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }),
    );
    fixture.detectChanges();
    expect(input.getAttribute('aria-activedescendant')).toBe('title-autocomplete-option-1');
    input.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }),
    );
    fixture.detectChanges();
    expect(selected).toEqual([{ id: 'title-2', name: 'Training B' }]);
    expect(input.getAttribute('aria-expanded')).toBe('false');
  });

  it('selects the active listbox option on Tab without preventing focus movement', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    component.onFocus();
    component.onSearchInput({ target: { value: 'Training' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [{ node: { id: 'title-1', value: 'Training A' } }],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const tab = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    input.dispatchEvent(tab);
    fixture.detectChanges();

    expect(tab.defaultPrevented).toBe(false);
    expect(selected).toEqual([{ id: 'title-1', name: 'Training A' }]);
    expect(input.getAttribute('aria-expanded')).toBe('false');
  });

  it('allows Tab focus movement when selecting an exact match from a closed dropdown', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    component.onFocus();
    component.onSearchInput({ target: { value: 'Training' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [{ node: { id: 'title-1', value: 'Training' } }],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }),
    );
    fixture.detectChanges();
    expect(input.getAttribute('aria-expanded')).toBe('false');

    const tab = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    input.dispatchEvent(tab);
    fixture.detectChanges();

    expect(tab.defaultPrevented).toBe(false);
    expect(selected).toEqual([{ id: 'title-1', name: 'Training' }]);
  });

  it('announces a successful title search with no matches', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    component.onFocus();
    component.onSearchInput({ target: { value: 'Missing title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    const status = fixture.nativeElement.querySelector(
      '#title-autocomplete-empty-results',
    ) as HTMLElement;
    expect(input.getAttribute('aria-expanded')).toBe('true');
    expect(input.getAttribute('aria-controls')).toBe(status.id);
    expect(status.getAttribute('role')).toBe('status');
    expect(status.getAttribute('aria-live')).toBe('polite');
  });

  it('does not offer manual fallback on lookup failure and enables it after a successful no-match', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));

    component.onFocus();
    component.onSearchInput({ target: { value: 'New title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    component.onManualEntry();
    expect(selected).toHaveLength(0);

    results[0].next({ loading: false, error: new Error('lookup failed') });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const failedText = fixture.nativeElement.textContent as string;
    expect(failedText).toContain('Could not search titles.');
    expect(failedText).not.toContain('No existing titles found');
    component.onManualEntry();
    expect(selected).toHaveLength(0);

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    await vi.waitFor(() => expect(watch).toHaveBeenCalledTimes(2));
    results[1].next({
      loading: false,
      data: { refTestTitles: { edges: [], pageInfo: { hasNextPage: false, endCursor: null } } },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No existing titles found');
    component.onManualEntry();
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledOnce());
    exactResults[0].next({
      loading: false,
      data: { refTestTitles: { edges: [], pageInfo: { hasNextPage: false, endCursor: null } } },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(selected).toEqual([{ name: 'New title' }]);
  });

  it('creates a new title after partial matches but keeps exact-match selection on blur', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));

    component.onFocus();
    component.onSearchInput({ target: { value: 'New title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [{ node: { id: 'partial-title', value: 'A New title for 2026' } }],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    component.onBlur();
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledOnce());
    exactResults[0].next({
      loading: false,
      data: { refTestTitles: { edges: [], pageInfo: { hasNextPage: false, endCursor: null } } },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await vi.waitFor(() => expect(selected).toEqual([{ name: 'New title' }]));

    component.onSearchInput({ target: { value: 'Existing title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledTimes(2));
    results[1].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [{ node: { id: 'existing-title', value: 'Existing title' } }],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    component.onBlur();
    await vi.waitFor(() =>
      expect(selected).toEqual([
        { name: 'New title' },
        { id: 'existing-title', name: 'Existing title' },
      ]),
    );
  });

  it('checks for an exact title before fallback when contains suggestions have more pages', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));

    component.onFocus();
    component.onSearchInput({ target: { value: 'New title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: Array.from({ length: 10 }, (_, index) => ({
            node: { id: `partial-${index}`, value: `New title match ${index}` },
          })),
          pageInfo: { hasNextPage: true, endCursor: 'cursor-10' },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    component.onBlur();
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledOnce());
    expect(selected).toEqual([]);
    expect(fetch).toHaveBeenCalledWith(
      expect.objectContaining({
        variables: {
          first: 1,
          where: { value: { eq: 'New title' } },
          order: { value: 'ASC' },
        },
        fetchPolicy: 'network-only',
      }),
    );

    exactResults[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [{ node: { id: 'later-exact-title', value: 'New title' } }],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(selected).toEqual([{ id: 'later-exact-title', name: 'New title' }]);
  });

  it('does not allow fallback when the exact-title lookup fails', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));

    component.onFocus();
    component.onSearchInput({ target: { value: 'New title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [{ node: { id: 'partial-title', value: 'A New title' } }],
          pageInfo: { hasNextPage: true, endCursor: 'cursor-10' },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    component.onBlur();
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledOnce());
    exactResults[0].next({ loading: false, error: new Error('exact lookup failed') });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.searchError()).toBe(true), { timeout: 2000 });
    expect(component.suggestions()).toEqual([]);
    expect(selected).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Could not search titles.');
    expect(fixture.nativeElement.textContent).not.toContain('No existing titles found');
  });

  it('resolves a blurred title after a delayed successful search', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));

    component.onFocus();
    component.onSearchInput({ target: { value: 'Delayed title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    component.onBlur();
    await new Promise((resolve) => setTimeout(resolve, 450));
    expect(selected).toEqual([]);
    expect(fetch).not.toHaveBeenCalled();

    results[0].next({
      loading: false,
      data: {
        refTestTitles: {
          edges: [],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await vi.waitFor(() => expect(fetch).toHaveBeenCalledOnce());
    expect(selected).toEqual([]);

    exactResults[0].next({
      loading: false,
      data: { refTestTitles: { edges: [], pageInfo: { hasNextPage: false, endCursor: null } } },
    });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(selected).toEqual([{ name: 'Delayed title' }]);
  });

  it('does not resolve a blurred title when the delayed search fails', async () => {
    const fixture: ComponentFixture<TitleAutocomplete> = TestBed.createComponent(TitleAutocomplete);
    fixture.detectChanges();
    const component = fixture.componentInstance as unknown as ITitleAutocompleteHarness;
    const selected: Array<{ id?: string; name: string }> = [];
    fixture.componentInstance.selectTitle.subscribe((title) => selected.push(title));

    component.onFocus();
    component.onSearchInput({ target: { value: 'Failed title' } } as unknown as Event);
    await vi.waitFor(() => expect(watch).toHaveBeenCalledOnce());
    component.onBlur();
    await new Promise((resolve) => setTimeout(resolve, 450));

    results[0].next({ loading: false, error: new Error('lookup failed') });
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(selected).toEqual([]);
    expect(fetch).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Could not search titles.');
    expect(fixture.nativeElement.textContent).not.toContain('No existing titles found');
  });
});
