export interface IQuestion {
  id: string;
  number: string;
  phrase: Record<string, string>;
  answers: IAnswer[];
}

export interface IAnswer {
  id: string;
  number: string;
  phrase: Record<string, string>;
  isCorrect: boolean;
}
