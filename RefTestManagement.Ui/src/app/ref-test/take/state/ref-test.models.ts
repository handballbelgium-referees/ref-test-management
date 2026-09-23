export interface Answer {
  id: string;
  number?: string | null;
  phrase: Record<string, string>;
  isCorrect?: boolean;
}

export interface Question {
  id: string;
  number?: string;
  phrase: Record<string, string>;
  answers: Answer[];
}

export interface RefTestResult {
  questionScore?: number | null;
  questionTotal?: number | null;
  answerScore?: number | null;
  answerTotal?: number | null;
  percentage?: number | null;
  sendResultsAutomatically?: boolean | null;
  resultsSent?: boolean | null;
}
