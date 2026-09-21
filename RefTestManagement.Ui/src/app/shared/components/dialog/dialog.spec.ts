import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Dialog } from './dialog';

@Component({
  imports: [Dialog],
  template: `
    <button #trigger type="button" (click)="open.set(true)">Open</button>
    @if (open()) {
      <div class="backdrop" (click)="open.set(false)">
        <div appDialog>
          <h2>Test dialog</h2>
          <button type="button">First</button>
          <button type="button">Last</button>
        </div>
      </div>
    }
  `,
})
class DialogHost {
  open = signal(false);
}

describe('Dialog', () => {
  let fixture: ComponentFixture<DialogHost>;
  let getClientRects: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    fixture = TestBed.createComponent(DialogHost);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('button') as HTMLButtonElement).focus();

    getClientRects = vi
      .spyOn(HTMLElement.prototype, 'getClientRects')
      .mockImplementation(() => ({ length: 1 }) as DOMRectList);
    fixture.componentInstance.open.set(true);
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    getClientRects.mockRestore();
  });

  it('provides modal semantics and focuses the first control', async () => {
    await new Promise<void>((resolve) => queueMicrotask(resolve));

    const dialog = fixture.nativeElement.querySelector('[appDialog]') as HTMLElement;
    expect(dialog.getAttribute('role')).toBe('dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBeTruthy();
    expect(document.activeElement).toBe(dialog.querySelector('button'));
  });

  it('traps reverse tabbing and restores focus after Escape closes', async () => {
    await new Promise<void>((resolve) => queueMicrotask(resolve));

    const dialog = fixture.nativeElement.querySelector('[appDialog]') as HTMLElement;
    const buttons = dialog.querySelectorAll('button');
    buttons[0].focus();
    dialog.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Tab', shiftKey: true, bubbles: true }),
    );

    expect(document.activeElement).toBe(buttons[1]);

    dialog.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();

    expect(fixture.componentInstance.open()).toBe(false);
    expect(document.activeElement).toBe(fixture.nativeElement.querySelector('button'));
  });
});
