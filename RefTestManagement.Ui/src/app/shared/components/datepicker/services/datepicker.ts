import { Service, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { CalendarDay, MonthItem, ViewMode, YearItem } from '../models/datepicker.types';

@Service({ autoProvided: false })
export class Datepicker {
  private readonly _translate = inject(TranslateService);

  formatDate(date: Date): string {
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const year = date.getFullYear();
    return `${day}/${month}/${year}`;
  }

  parseDate(value: string): Date | null {
    const parts = value.split('/');
    if (parts.length !== 3) return null;

    const day = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10) - 1;
    const year = parseInt(parts[2], 10);

    const date = new Date(year, month, day);
    if (isNaN(date.getTime()) || date.getDate() !== day || date.getMonth() !== month) {
      return null;
    }

    date.setHours(0, 0, 0, 0);
    return date;
  }

  isSameDay(date1: Date, date2: Date): boolean {
    return (
      date1.getFullYear() === date2.getFullYear() &&
      date1.getMonth() === date2.getMonth() &&
      date1.getDate() === date2.getDate()
    );
  }

  generateCalendarDays(currentDate: Date, selectedDate: Date | null, today: Date): CalendarDay[] {
    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();

    const firstDay = new Date(year, month, 1);
    const lastDay = new Date(year, month + 1, 0);
    const prevLastDay = new Date(year, month, 0);

    const firstDayOfWeek = firstDay.getDay() === 0 ? 6 : firstDay.getDay() - 1;
    const lastDate = lastDay.getDate();
    const prevLastDate = prevLastDay.getDate();

    const days: CalendarDay[] = [];

    // Previous month days
    for (let i = firstDayOfWeek; i > 0; i--) {
      const date = new Date(year, month - 1, prevLastDate - i + 1);
      days.push({
        date,
        day: date.getDate(),
        isCurrentMonth: false,
        isToday: this.isSameDay(date, today),
        isSelected: selectedDate ? this.isSameDay(date, selectedDate) : false,
        isDisabled: false,
      });
    }

    // Current month days
    for (let day = 1; day <= lastDate; day++) {
      const date = new Date(year, month, day);
      days.push({
        date,
        day,
        isCurrentMonth: true,
        isToday: this.isSameDay(date, today),
        isSelected: selectedDate ? this.isSameDay(date, selectedDate) : false,
        isDisabled: false,
      });
    }

    // Next month days to complete the last week
    const totalDays = days.length;
    const weeksNeeded = Math.ceil(totalDays / 7);
    const remainingDays = weeksNeeded * 7 - totalDays;

    for (let day = 1; day <= remainingDays; day++) {
      const date = new Date(year, month + 1, day);
      days.push({
        date,
        day,
        isCurrentMonth: false,
        isToday: this.isSameDay(date, today),
        isSelected: selectedDate ? this.isSameDay(date, selectedDate) : false,
        isDisabled: false,
      });
    }

    return days;
  }

  generateMonths(currentDate: Date): MonthItem[] {
    const months: MonthItem[] = [];
    for (let i = 0; i < 12; i++) {
      const date = new Date(2000, i, 1);
      months.push({
        index: i,
        name: date.toLocaleDateString(this._translate.currentLang, { month: 'short' }),
        isSelected: i === currentDate.getMonth(),
      });
    }
    return months;
  }

  generateYears(currentDate: Date): YearItem[] {
    const currentYear = currentDate.getFullYear();
    const startYear = Math.floor(currentYear / 10) * 10;
    const years: YearItem[] = [];

    for (let i = startYear - 1; i < startYear + 11; i++) {
      years.push({
        year: i,
        isSelected: i === currentYear,
        isOutOfRange: i < startYear || i >= startYear + 10,
      });
    }

    return years;
  }

  getWeekDays(): string[] {
    const days: string[] = [];
    const baseDate = new Date(2024, 0, 1); // Monday, January 1, 2024

    for (let i = 0; i < 7; i++) {
      const date = new Date(baseDate);
      date.setDate(baseDate.getDate() + i);
      days.push(date.toLocaleDateString(this._translate.currentLang, { weekday: 'short' }));
    }

    return days;
  }

  getViewTitle(date: Date, mode: ViewMode): string {
    if (mode === 'days') {
      return date.toLocaleDateString(this._translate.currentLang, {
        month: 'long',
        year: 'numeric',
      });
    } else if (mode === 'months') {
      return date.getFullYear().toString();
    } else {
      const startYear = Math.floor(date.getFullYear() / 10) * 10;
      return `${startYear} - ${startYear + 9}`;
    }
  }

  detectSmallTouchDevice(): boolean {
    const hasTouch =
      'ontouchstart' in window ||
      navigator.maxTouchPoints > 0 ||
      (navigator as any).msMaxTouchPoints > 0;

    const isSmallScreen = window.innerWidth < 1024;

    return hasTouch && isSmallScreen;
  }
}
