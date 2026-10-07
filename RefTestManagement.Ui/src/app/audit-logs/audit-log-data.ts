import { computed, inject, Service, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, Subject, switchMap } from 'rxjs';
import {
  AuditLogDtoFilterInput,
  AuditLogDtoSortInput,
  GetAuditLogsGQL,
  SortEnumType,
} from '../../../graphql/generated';

const PAGE_SIZE = 20;

@Service()
export class AuditLogData {
  private readonly _getAuditLogsGQL = inject(GetAuditLogsGQL);
  private readonly _queryRefresh = new Subject<void>();
  private readonly _retryError = signal<unknown | null>(null);

  readonly filter = signal<AuditLogDtoFilterInput | null>(null);
  readonly sortField = signal<string>('seqId');
  readonly sortDirection = signal<SortEnumType>('DESC');
  readonly loadingMore = signal(false);
  readonly loadMoreError = signal<unknown | null>(null);

  private readonly _queryRef = this._getAuditLogsGQL.watch({
    variables: {
      first: PAGE_SIZE,
      order: this._buildOrder(),
      where: this.filter(),
    },
  });
  private readonly _lastData = signal(this._queryRef.getCurrentResult().data?.auditLogs ?? null);

  readonly queryResult = toSignal(
    this._queryRefresh.pipe(
      startWith(undefined),
      switchMap(() =>
        this._queryRef.valueChanges.pipe(
          map((result) => {
            if (result.data?.auditLogs) this._lastData.set(result.data.auditLogs);
            this._retryError.set(result.error ?? null);
            return {
              data: result.data?.auditLogs ?? this._lastData(),
              loading: result.loading,
              error: result.error ?? null,
            };
          }),
          catchError((error: unknown) => {
            this._retryError.set(error);
            return of({ data: this._lastData(), loading: false, error });
          }),
        ),
      ),
    ),
    { initialValue: { data: null, loading: true, error: null } },
  );

  readonly queryError = computed(() => this._retryError() ?? this.queryResult().error);
  readonly loading = computed(() => !this.queryError() && this.queryResult().loading);

  applyFilter(where: AuditLogDtoFilterInput | null): void {
    this.filter.set(where);
    this._queryRef.setVariables({ first: PAGE_SIZE, order: this._buildOrder(), where });
    this._lastData.set(null);
    this.loadMoreError.set(null);
    this._queryRefresh.next();
  }

  applySort(field: string): void {
    if (this.sortField() === field) {
      this.sortDirection.update((d) => (d === 'ASC' ? 'DESC' : 'ASC'));
    } else {
      this.sortField.set(field);
      this.sortDirection.set('DESC');
    }
    this._queryRef.setVariables({
      first: PAGE_SIZE,
      order: this._buildOrder(),
      where: this.filter(),
    });
    this._lastData.set(null);
    this.loadMoreError.set(null);
    this._queryRefresh.next();
  }

  applyFullSort(field: string, direction: SortEnumType): void {
    this.sortField.set(field);
    this.sortDirection.set(direction);
    this._queryRef.setVariables({
      first: PAGE_SIZE,
      order: this._buildOrder(),
      where: this.filter(),
    });
    this._lastData.set(null);
    this.loadMoreError.set(null);
    this._queryRefresh.next();
  }

  loadMore(): void {
    const pageInfo = this.queryResult().data?.pageInfo;
    if (!pageInfo?.hasNextPage || this.loadingMore()) return;

    this.loadingMore.set(true);
    this.loadMoreError.set(null);
    void this._queryRef
      .fetchMore({
        variables: {
          first: PAGE_SIZE,
          after: pageInfo.endCursor,
          order: this._buildOrder(),
          where: this.filter(),
        },
      })
      .then(() => this.loadingMore.set(false))
      .catch((error: unknown) => {
        this.loadingMore.set(false);
        this.loadMoreError.set(error);
      });
  }

  retry(): void {
    this._queryRefresh.next();
    void this._queryRef.refetch().catch((error: unknown) => this._retryError.set(error));
  }

  refresh(): void {
    this.retry();
  }

  private _buildOrder(): AuditLogDtoSortInput[] {
    return [{ [this.sortField()]: this.sortDirection() } as AuditLogDtoSortInput];
  }
}
