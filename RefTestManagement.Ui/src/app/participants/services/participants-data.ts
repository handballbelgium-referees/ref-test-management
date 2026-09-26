import { computed, inject, Service } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { GetParticipantsGQL, GetParticipantsQuery } from '../../../../graphql/generated';

export type ParticipantListItem = GetParticipantsQuery['participants']['edges'][number]['node'];

@Service()
export class ParticipantsData {
  private readonly _getParticipantsGQL = inject(GetParticipantsGQL);

  private readonly _queryRef = this._getParticipantsGQL.watch({
    first: 100,
    order: [{ lastName: 'ASC' }, { firstName: 'ASC' }],
  });

  readonly queryResult = toSignal(this._queryRef.valueChanges);

  readonly loading = computed(() => this.queryResult()?.loading ?? false);

  readonly participants = computed((): ParticipantListItem[] => {
    return this.queryResult()?.data?.participants?.edges?.map((edge) => edge.node) ?? [];
  });

  async refresh(): Promise<void> {
    await this._queryRef.refetch({
      first: 100,
      order: [{ lastName: 'ASC' }, { firstName: 'ASC' }],
    });
  }
}
