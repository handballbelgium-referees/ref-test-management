import { ParticipantLevel, ParticipantType } from '../../../../graphql/generated';

export interface IParticipantFormValue {
  firstName: string;
  lastName: string;
  email: string;
  type: ParticipantType;
  level: ParticipantLevel | null;
}

export const PARTICIPANT_TYPE_OPTIONS: Array<{
  value: ParticipantType;
  labelKey: string;
}> = [
  { value: 'REFEREE', labelKey: 'participants.types.referee' },
  { value: 'DELEGATE', labelKey: 'participants.types.delegate' },
  { value: 'OTHER', labelKey: 'participants.types.other' },
];

export const PARTICIPANT_LEVEL_OPTIONS: Array<{
  value: ParticipantLevel;
  labelKey: string;
}> = [
  { value: 'ELITE', labelKey: 'participants.levels.elite' },
  { value: 'NATIONAL_PLUS', labelKey: 'participants.levels.national_plus' },
  { value: 'NATIONAL', labelKey: 'participants.levels.national' },
  { value: 'LEAGUE', labelKey: 'participants.levels.league' },
  { value: 'REGIONAL', labelKey: 'participants.levels.regional' },
];

export const createEmptyParticipant = (): IParticipantFormValue => ({
  firstName: '',
  lastName: '',
  email: '',
  type: 'OTHER',
  level: null,
});

export function isParticipantDraftEmpty(participant: IParticipantFormValue): boolean {
  return (
    participant.firstName.trim().length === 0 &&
    participant.lastName.trim().length === 0 &&
    participant.email.trim().length === 0
  );
}
