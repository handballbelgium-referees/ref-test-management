import { inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { onlyCompleteData } from 'apollo-angular';
import { map } from 'rxjs';
import {
  AuditLogDtoFilterInput,
  AuditLogDtoSortInput,
  GetAuditLogsGQL,
} from '../../../graphql/generated';

const PAGE_SIZE = 20;

const DEFAULT_ORDER: AuditLogDtoSortInput[] = [{ timestamp: 'DESC' }];

@Injectable({ providedIn: 'root' })
export class AuditLogData {
  private readonly _getAuditLogsGQL = inject(GetAuditLogsGQL);

  private readonly _queryRef = this._getAuditLogsGQL.watch({
    variables: {
      first: PAGE_SIZE,
      order: DEFAULT_ORDER,
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

  readonly filter = signal<AuditLogDtoFilterInput | null>(null);

  applyFilter(where: AuditLogDtoFilterInput | null): void {
    this.filter.set(where);
    this._queryRef.setVariables({ first: PAGE_SIZE, order: DEFAULT_ORDER, where });
  }

  loadMore(): void {
    const pageInfo = this.queryResult()?.pageInfo;
    if (!pageInfo?.hasNextPage) return;

    this._queryRef.fetchMore({
      variables: {
        first: PAGE_SIZE,
        after: pageInfo.endCursor,
        order: DEFAULT_ORDER,
        where: this.filter(),
      },
    });
  }

  refresh(): void {
    this._queryRef.refetch();
  }
}
