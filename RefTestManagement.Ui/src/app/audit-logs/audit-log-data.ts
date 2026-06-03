import { inject, Service, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { onlyCompleteData } from 'apollo-angular';
import { map } from 'rxjs';
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

  readonly filter = signal<AuditLogDtoFilterInput | null>(null);
  readonly sortField = signal<string>('seqId');
  readonly sortDirection = signal<SortEnumType>('DESC');

  private readonly _queryRef = this._getAuditLogsGQL.watch({
    variables: {
      first: PAGE_SIZE,
      order: this._buildOrder(),
    },
  });

  readonly queryResult = toSignal(
    this._queryRef.valueChanges.pipe(
      onlyCompleteData(),
      map((result) => result.data.auditLogs),
    ),
  );

  readonly loading = toSignal(this._queryRef.valueChanges.pipe(map((r) => r.loading)), {
    initialValue: true,
  });

  applyFilter(where: AuditLogDtoFilterInput | null): void {
    this.filter.set(where);
    this._queryRef.setVariables({ first: PAGE_SIZE, order: this._buildOrder(), where });
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
  }

  applyFullSort(field: string, direction: SortEnumType): void {
    this.sortField.set(field);
    this.sortDirection.set(direction);
    this._queryRef.setVariables({
      first: PAGE_SIZE,
      order: this._buildOrder(),
      where: this.filter(),
    });
  }

  loadMore(): void {
    const pageInfo = this.queryResult()?.pageInfo;
    if (!pageInfo?.hasNextPage) return;

    this._queryRef.fetchMore({
      variables: {
        first: PAGE_SIZE,
        after: pageInfo.endCursor,
        order: this._buildOrder(),
        where: this.filter(),
      },
    });
  }

  refresh(): void {
    this._queryRef.refetch();
  }

  private _buildOrder(): AuditLogDtoSortInput[] {
    return [{ [this.sortField()]: this.sortDirection() } as AuditLogDtoSortInput];
  }
}
