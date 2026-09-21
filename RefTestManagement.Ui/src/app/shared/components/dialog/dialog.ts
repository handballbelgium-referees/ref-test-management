import { DOCUMENT } from '@angular/common';
import {
  AfterViewInit,
  Directive,
  ElementRef,
  HostBinding,
  HostListener,
  Inject,
  OnDestroy,
} from '@angular/core';

const focusableSelector = [
  'a[href]',
  'area[href]',
  'button:not([disabled])',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[contenteditable="true"]',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

let dialogId = 0;

@Directive({
  selector: '[appDialog]',
})
export class Dialog implements AfterViewInit, OnDestroy {
  @HostBinding('attr.role') protected readonly role = 'dialog';
  @HostBinding('attr.aria-modal') protected readonly modal = 'true';
  @HostBinding('attr.tabindex') protected readonly tabindex = '-1';

  private readonly previousFocus: HTMLElement | null;

  constructor(
    private readonly elementRef: ElementRef<HTMLElement>,
    @Inject(DOCUMENT) private readonly document: Document,
  ) {
    this.previousFocus =
      this.document.activeElement instanceof HTMLElement ? this.document.activeElement : null;
  }

  ngAfterViewInit(): void {
    const heading = this.elementRef.nativeElement.querySelector<HTMLElement>('h1, h2, h3');
    if (heading) {
      const id = heading.id || `dialog-title-${++dialogId}`;
      heading.id = id;
      this.elementRef.nativeElement.setAttribute('aria-labelledby', id);
    } else {
      this.elementRef.nativeElement.setAttribute('aria-label', 'Dialog');
    }

    queueMicrotask(() => this.focusFirstElement());
  }

  ngOnDestroy(): void {
    if (this.previousFocus?.isConnected) {
      this.previousFocus.focus();
    }
  }

  @HostListener('keydown', ['$event'])
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.elementRef.nativeElement.parentElement?.click();
      return;
    }

    if (event.key !== 'Tab') return;

    const focusable = this.getFocusableElements();
    if (focusable.length === 0) {
      event.preventDefault();
      this.elementRef.nativeElement.focus();
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = this.document.activeElement;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusFirstElement(): void {
    const first = this.getFocusableElements()[0];
    (first ?? this.elementRef.nativeElement).focus();
  }

  private getFocusableElements(): HTMLElement[] {
    return Array.from(
      this.elementRef.nativeElement.querySelectorAll<HTMLElement>(focusableSelector),
    ).filter((element) => element.getClientRects().length > 0);
  }
}
