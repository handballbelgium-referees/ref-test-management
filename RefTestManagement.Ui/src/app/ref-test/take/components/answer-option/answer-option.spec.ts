import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AnswerOption } from './answer-option';

describe('AnswerOption', () => {
  let fixture: ComponentFixture<AnswerOption>;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [AnswerOption] });
    fixture = TestBed.createComponent(AnswerOption);
    fixture.componentRef.setInput('phrase', { en: 'Answer' });
    fixture.componentRef.setInput('selected', true);
    fixture.componentRef.setInput('currentLanguage', 'en');
  });

  afterEach(() => TestBed.resetTestingModule());

  it('exposes selection state on its native button', async () => {
    await fixture.whenStable();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.type).toBe('button');
    expect(button.tabIndex).toBe(0);
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(button.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');

    fixture.componentRef.setInput('selected', false);
    await fixture.whenStable();
    expect(button.getAttribute('aria-pressed')).toBe('false');
  });
});
