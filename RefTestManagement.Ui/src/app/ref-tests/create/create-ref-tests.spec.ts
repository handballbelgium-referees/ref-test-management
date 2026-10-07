import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WritableSignal } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CreateRefTestsGQL, GetQuestionsByNumberGQL } from '../../../../graphql/generated';
import { Banner } from '../../services/banner';
import { CreateRefTests } from './create-ref-tests';

interface ICreateModel {
  users: Array<{ firstName: string; lastName: string; email: string }>;
  title: { id?: string; name: string } | null;
  numberOfQuestions: number;
  randomQuestionsForEachUser: boolean;
  maxTimeInMinutes: number;
  specificQuestionNumbers: string;
  sendInvitations: boolean;
  sendResults: boolean;
  scheduledAt: string;
}

interface IQuestionImportResult {
  data?: {
    questionsByNumber?: Array<{
      number: string;
      phrase?: Record<string, string> | null;
    }> | null;
  };
  error?: unknown;
}

interface ICreateRefTestsHarness {
  refTestModel: WritableSignal<ICreateModel>;
  loading: () => boolean;
  loadingQuestions: () => boolean;
  questionImportError: () => boolean;
  showQuestionImport: WritableSignal<boolean>;
  onSubmit(): void;
  onQuestionImport(text: string): void;
  retryQuestionImport(): void;
}

describe('CreateRefTests', () => {
  let mutation = new Subject<unknown>();
  const questionImportResults: Subject<IQuestionImportResult>[] = [];
  const mutate = vi.fn(() => mutation.asObservable());
  const fetchQuestions = vi.fn(() => {
    const result = new Subject<IQuestionImportResult>();
    questionImportResults.push(result);
    return result.asObservable();
  });
  const banner = { error: vi.fn(), success: vi.fn() };

  beforeEach(() => {
    mutation = new Subject<unknown>();
    questionImportResults.length = 0;
    mutate.mockClear();
    fetchQuestions.mockClear();
    banner.error.mockClear();
    banner.success.mockClear();

    TestBed.configureTestingModule({
      imports: [CreateRefTests],
      providers: [
        provideRouter([]),
        provideTranslateService({ fallbackLang: 'en' }),
        { provide: CreateRefTestsGQL, useValue: { mutate } },
        { provide: GetQuestionsByNumberGQL, useValue: { fetch: fetchQuestions } },
        { provide: Banner, useValue: banner },
      ],
    }).overrideComponent(CreateRefTests, { set: { imports: [], template: '' } });
  });

  afterEach(() => TestBed.resetTestingModule());

  function render(): { fixture: ComponentFixture<CreateRefTests>; component: ICreateRefTestsHarness } {
    const fixture = TestBed.createComponent(CreateRefTests);
    const component = fixture.componentInstance as unknown as ICreateRefTestsHarness;
    fixture.detectChanges();
    return { fixture, component };
  }

  it('keeps mutation loading active and ignores duplicate submits until the request completes', () => {
    const { component } = render();
    component.refTestModel.set({
      users: [{ firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.test' }],
      title: { name: 'Training' },
      numberOfQuestions: 30,
      randomQuestionsForEachUser: false,
      maxTimeInMinutes: 60,
      specificQuestionNumbers: '',
      sendInvitations: false,
      sendResults: false,
      scheduledAt: '',
    });

    component.onSubmit();
    component.onSubmit();

    expect(mutate).toHaveBeenCalledOnce();
    expect(component.loading()).toBe(true);

    mutation.error(new Error('creation failed'));
    expect(component.loading()).toBe(false);
    expect(banner.error).toHaveBeenCalledWith('creation failed');
  });

  it('shows question lookup failures and retries the retained import', () => {
    const { component } = render();
    component.showQuestionImport.set(true);
    component.onQuestionImport('1.1, 2.2');

    expect(component.loadingQuestions()).toBe(true);
    questionImportResults[0].next({ error: new Error('lookup failed') });
    expect(component.loadingQuestions()).toBe(false);
    expect(component.questionImportError()).toBe(true);

    component.retryQuestionImport();
    expect(fetchQuestions).toHaveBeenCalledTimes(2);
    expect(fetchQuestions).toHaveBeenLastCalledWith({ variables: { numbers: ['1.1', '2.2'] } });
    questionImportResults[1].next({
      data: { questionsByNumber: [{ number: '1.1', phrase: { en: 'Question' } }] },
    });

    expect(component.questionImportError()).toBe(false);
    expect(component.showQuestionImport()).toBe(false);
  });
});
