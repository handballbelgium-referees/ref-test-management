export const REF_TEST_CONFIG = {
  PAGE_SIZE: 20,
  SCROLL_THRESHOLD: 200,
  SEARCH_DEBOUNCE_MS: 300,
  REPORT_BANNER_TIMEOUT_MS: 10000,
};

export const COLUMNS = {
  TITLE: 'title',
  PARTICIPANT: 'participant',
  STATUS: 'status',
  QUESTIONS: 'questions',
  MAX_TIME: 'maxTimeInMinutes',
  SCORE: 'score',
  INVITATION: 'invitation',
  RESULTS: 'results',
  STARTED: 'started',
  COMPLETED: 'completed',
};

export const PERCENTAGE_RANGES = {
  LOW: { min: 0, max: 50 },
  MEDIUM: { min: 50, max: 75 },
  HIGH: { min: 75, max: 100 },
};
