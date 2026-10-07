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
