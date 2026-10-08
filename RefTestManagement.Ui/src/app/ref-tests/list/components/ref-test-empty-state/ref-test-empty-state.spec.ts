import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { RefTestEmptyState } from './ref-test-empty-state';

@Component({
  imports: [RefTestEmptyState],
  template: `
    <app-ref-test-empty-state
      [filtered]="filtered"
      (createClick)="onCreate()"
      (clearFiltersClick)="onClearFilters()"
    />
  `,
})
class EmptyStateHost {
  filtered = false;
  created = 0;
  cleared = 0;

  onCreate(): void {
    this.created += 1;
  }

  onClearFilters(): void {
    this.cleared += 1;
  }
}

describe('RefTestEmptyState', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [EmptyStateHost],
      providers: [provideTranslateService({ fallbackLang: 'en' })],
    });
    TestBed.inject(TranslateService).setTranslation(
      'en',
      {
        ref_tests: {
          list: {
            empty: {
              title: 'No RefTests found',
              subtitle: 'Create your first RefTest',
              action: 'Create RefTests',
              filtered_title: 'No matching RefTests',
              filtered_subtitle: 'Try changing or clearing the filters.',
              clear_filters: 'Clear filters',
            },
          },
        },
      },
      true,
    );
  });

  afterEach(() => TestBed.resetTestingModule());

  it('offers filter recovery instead of first-item creation copy for filtered results', async () => {
    const fixture: ComponentFixture<EmptyStateHost> = TestBed.createComponent(EmptyStateHost);
    fixture.componentInstance.filtered = true;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('No matching RefTests');
    expect(text).toContain('Try changing or clearing the filters.');
    expect(text).not.toContain('first RefTest');
    expect(fixture.nativeElement.querySelector('button')?.textContent).toContain('Clear filters');

    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    expect(fixture.componentInstance.cleared).toBe(1);
    expect(fixture.componentInstance.created).toBe(0);
  });

  it('keeps the create-first action for an unfiltered empty list', async () => {
    const fixture = TestBed.createComponent(EmptyStateHost);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Create your first RefTest');
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).click();
    expect(fixture.componentInstance.created).toBe(1);
    expect(fixture.componentInstance.cleared).toBe(0);
  });
});
