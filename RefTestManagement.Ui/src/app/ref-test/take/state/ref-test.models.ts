export interface Answer {
  id: string;
  phrase: Record<string, string>;
}

export interface Question {
  id: string;
  phrase: Record<string, string>;
  answers: Answer[];
}

export interface RefTestResult {
  questionScore?: number | null;
  questionTotal?: number | null;
  answerScore?: number | null;
  answerTotal?: number | null;
  percentage?: number | null;
}
