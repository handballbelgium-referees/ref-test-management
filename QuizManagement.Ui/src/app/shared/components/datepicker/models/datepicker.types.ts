export interface CalendarDay {
  date: Date;
  day: number;
  isCurrentMonth: boolean;
  isToday: boolean;
  isSelected: boolean;
  isDisabled: boolean;
}

export interface MonthItem {
  index: number;
  name: string;
  isSelected: boolean;
}

export interface YearItem {
  year: number;
  isSelected: boolean;
  isOutOfRange: boolean;
}

export type ViewMode = 'days' | 'months' | 'years';
