import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { BehaviorSubject, of, Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  AcknowledgeFailedPrivacyWithdrawalTargetGQL,
  GetFailedPrivacyWithdrawalTargetsGQL,
} from '../../../../../graphql/generated';
import { PrivacyWithdrawalReview } from './privacy-withdrawal-review';

const ACTION_TOKEN = 'a26de8c1-8d8e-4a41-a54a-fcfb88a3462a';
const REF_TEST_ID = 'cf47634f-1ef5-49d9-a1b1-895a94f0cf3c';
const REMAINING_ACTION_TOKEN = 'b12e4330-a836-4c97-b1eb-81686e4f6cb9';

interface FailedTargetNode {
  actionToken: string;
  failureCategory: string;
}

interface ReviewQueryResult {
  data?: {
    failedPrivacyWithdrawalTargets?: {
      edges?: Array<{ node?: FailedTargetNode | null } | null> | null;
      pageInfo?: { hasNextPage: boolean; endCursor: string | null } | null;
    } | null;
  } | null;
  loading: boolean;
  error?: unknown;
}

type ReviewQueryData = NonNullable<ReviewQueryResult['data']>;

interface FetchMoreOptions {
  updateQuery: (
    previous: ReviewQueryData,
    result: { fetchMoreResult?: ReviewQueryData },
  ) => ReviewQueryData;
}

describe('PrivacyWithdrawalReview', () => {
  let fixture: ComponentFixture<PrivacyWithdrawalReview>;
  let queryResults: BehaviorSubject<ReviewQueryResult>;
  let refetchQuery: ReturnType<typeof vi.fn>;
  let fetchMoreQuery: ReturnType<typeof vi.fn>;
  let acknowledgeMutation: ReturnType<typeof vi.fn>;
  let acknowledgementStatus: 'ACKNOWLEDGED' | 'NOT_AVAILABLE';

  beforeEach(async () => {
    acknowledgementStatus = 'ACKNOWLEDGED';
    queryResults = new BehaviorSubject<ReviewQueryResult>({
      loading: false,
      data: {
        failedPrivacyWithdrawalTargets: {
          edges: [
            {
              node: {
                actionToken: ACTION_TOKEN,
                failureCategory: 'ATTEMPT_LIMIT_REACHED',
              },
            },
          ],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      },
    });
    refetchQuery = vi.fn(() => {
      const data: ReviewQueryResult['data'] = {
        failedPrivacyWithdrawalTargets: {
          edges: [
            {
              node: {
                actionToken: REMAINING_ACTION_TOKEN,
                failureCategory: 'PROCESSING_FAILED',
              },
            },
          ],
          pageInfo: { hasNextPage: false, endCursor: null },
        },
      };
      queryResults.next({ loading: false, data });
      return Promise.resolve({ data });
    });
    fetchMoreQuery = vi.fn(() => Promise.resolve({ data: queryResults.value.data }));
    const queryRef = {
      valueChanges: queryResults,
      refetch: refetchQuery,
      fetchMore: fetchMoreQuery,
    };
    acknowledgeMutation = vi.fn(() =>
      of({
        data: {
          acknowledgeFailedPrivacyWithdrawalTarget: {
            privacyWithdrawalTargetAcknowledgementResult: { status: acknowledgementStatus },
          },
        },
      }),
    );

    TestBed.configureTestingModule({
      imports: [PrivacyWithdrawalReview],
      providers: [
        provideTranslateService({ fallbackLang: 'en' }),
        {
          provide: GetFailedPrivacyWithdrawalTargetsGQL,
          useValue: { watch: vi.fn(() => queryRef) },
        },
        {
          provide: AcknowledgeFailedPrivacyWithdrawalTargetGQL,
          useValue: { mutate: acknowledgeMutation },
        },
      ],
    });

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation(
      'en',
      {
        privacy: {
          withdrawalReview: {
            title: 'Failed privacy-withdrawal targets',
            description: 'Only sanitized failure categories are shown.',
            loading: 'Loading failed withdrawal targets...',
            queryError: 'Could not load failed withdrawal targets.',
            retry: 'Retry',
            empty: 'There are no failed withdrawal targets to review.',
            failureCategory: {
              PROCESSING_FAILED: 'Processing failed',
              ATTEMPT_LIMIT_REACHED: 'Retry limit reached',
            },
            acknowledge: 'Acknowledge and purge reference',
            acknowledging: 'Acknowledging...',
            acknowledgementError: 'Could not acknowledge this target.',
            acknowledged: 'Acknowledgement recorded; the operational reference was purged.',
            notAvailable: 'This failed target is no longer available.',
            loadMore: 'Load more',
            loadingMore: 'Loading more...',
            loadMoreError: 'Could not load more failed withdrawal targets.',
          },
        },
      },
      true,
    );
    await new Promise<void>((resolve) => translate.use('en').subscribe(() => resolve()));
    fixture = TestBed.createComponent(PrivacyWithdrawalReview);
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('shows only the sanitized failure category and never renders action or participant identifiers', () => {
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Failed privacy-withdrawal targets');
    expect(text).toContain('Retry limit reached');
    expect(text).not.toContain(ACTION_TOKEN);
    expect(text).not.toContain(REF_TEST_ID);
    expect(text).not.toContain('Participant Name');
    expect(text).not.toContain('participant@example.test');
  });

  it('acknowledges with the opaque token, then removes the target without displaying it', async () => {
    queryResults.next({
      loading: false,
      data: {
        failedPrivacyWithdrawalTargets: {
          edges: [
            {
              node: {
                actionToken: ACTION_TOKEN,
                failureCategory: 'ATTEMPT_LIMIT_REACHED',
              },
            },
          ],
          pageInfo: { hasNextPage: true, endCursor: 'cursor-before-acknowledgement' },
        },
      },
    });

    const button = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    ).find((candidate) => candidate.textContent?.includes('Acknowledge and purge reference'));
    expect(button).toBeDefined();

    button?.click();
    await vi.waitFor(() => expect(acknowledgeMutation).toHaveBeenCalledOnce());
    await vi.waitFor(() =>
      expect((fixture.nativeElement as HTMLElement).textContent).toContain(
        'Acknowledgement recorded; the operational reference was purged.',
      ),
    );

    expect(acknowledgeMutation).toHaveBeenCalledWith({
      variables: { input: { actionToken: ACTION_TOKEN } },
    });
    expect(refetchQuery).toHaveBeenCalledWith({ first: 20, after: null });
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(ACTION_TOKEN);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(
      'Retry limit reached',
    );
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Processing failed');
  });

  it('reports a stale target as not available and removes it from the review list', async () => {
    acknowledgementStatus = 'NOT_AVAILABLE';
    const button = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    ).find((candidate) => candidate.textContent?.includes('Acknowledge and purge reference'));

    button?.click();
    await vi.waitFor(() =>
      expect((fixture.nativeElement as HTMLElement).textContent).toContain(
        'This failed target is no longer available.',
      ),
    );

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(ACTION_TOKEN);
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain(
      'Retry limit reached',
    );
  });

  it('discards a page request that completes after acknowledgement refreshes the first page', async () => {
    const stalePage: ReviewQueryData = {
      failedPrivacyWithdrawalTargets: {
        edges: [
          {
            node: {
              actionToken: ACTION_TOKEN,
              failureCategory: 'ATTEMPT_LIMIT_REACHED',
            },
          },
          {
            node: {
              actionToken: 'c62f29b4-7595-42ee-9279-6340c9dcf42f',
              failureCategory: 'PROCESSING_FAILED',
            },
          },
        ],
        pageInfo: { hasNextPage: true, endCursor: 'stale-page-cursor' },
      },
    };
    let completeStalePageRequest: (() => void) | undefined;
    fetchMoreQuery.mockImplementationOnce((options: FetchMoreOptions) =>
      new Promise<{ data: ReviewQueryData }>((resolve) => {
        completeStalePageRequest = () => {
          const data = options.updateQuery(queryResults.value.data ?? {}, {
            fetchMoreResult: stalePage,
          });
          queryResults.next({ loading: false, data });
          resolve({ data });
        };
      }),
    );
    queryResults.next({
      loading: false,
      data: {
        failedPrivacyWithdrawalTargets: {
          edges: [
            {
              node: {
                actionToken: ACTION_TOKEN,
                failureCategory: 'ATTEMPT_LIMIT_REACHED',
              },
            },
          ],
          pageInfo: { hasNextPage: true, endCursor: 'cursor-before-acknowledgement' },
        },
      },
    });
    fixture.detectChanges();

    const buttons = () =>
      Array.from(
        (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
      );
    buttons()
      .find((button) => button.textContent?.includes('Load more'))
      ?.click();
    expect(fetchMoreQuery).toHaveBeenCalledOnce();

    const pendingAcknowledgement = new Subject<{
      data: {
        acknowledgeFailedPrivacyWithdrawalTarget: {
          privacyWithdrawalTargetAcknowledgementResult: { status: 'ACKNOWLEDGED' };
        };
      };
    }>();
    acknowledgeMutation.mockReturnValue(pendingAcknowledgement);
    buttons()
      .find((button) => button.textContent?.includes('Acknowledge and purge reference'))
      ?.click();
    fixture.detectChanges();

    const loadMoreButton = buttons().find((button) => button.textContent?.includes('Load more'));
    expect(loadMoreButton?.disabled).toBe(true);
    expect(loadMoreButton?.textContent).toContain('Load more');
    expect(loadMoreButton?.textContent).not.toContain('Loading more...');
    loadMoreButton?.click();
    expect(fetchMoreQuery).toHaveBeenCalledOnce();

    pendingAcknowledgement.next({
      data: {
        acknowledgeFailedPrivacyWithdrawalTarget: {
          privacyWithdrawalTargetAcknowledgementResult: { status: 'ACKNOWLEDGED' },
        },
      },
    });
    pendingAcknowledgement.complete();
    expect(refetchQuery).toHaveBeenCalledWith({ first: 20, after: null });

    expect(completeStalePageRequest).toBeDefined();
    completeStalePageRequest?.();
    fixture.detectChanges();
    await vi.waitFor(() => {
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(text).toContain('Processing failed');
      expect(text).not.toContain('Retry limit reached');
      expect(buttons().some((button) => button.textContent?.includes('Load more'))).toBe(false);
    });
  });
});
