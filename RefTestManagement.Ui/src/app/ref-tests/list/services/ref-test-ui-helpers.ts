import { Injectable } from '@angular/core';
import { RefTestStatus } from '../../../../../graphql/generated';

/**
 * Service providing UI utility methods for ref test display,
 * such as status class mapping and formatting helpers.
 */
@Injectable({ providedIn: 'root' })
export class RefTestUIHelpers {
  /**
   * Get the CSS class for a ref test status badge
   */
  getStatusClass(status: RefTestStatus): string {
    const statusClasses: Record<RefTestStatus, string> = {
      [RefTestStatus.Pending]: 'bg-yellow-100 text-yellow-800',
      [RefTestStatus.InProgress]: 'bg-blue-100 text-blue-800',
      [RefTestStatus.Completed]: 'bg-success-100 text-success-800',
      [RefTestStatus.Expired]: 'bg-red-100 text-red-800',
    };

    return statusClasses[status] ?? 'bg-neutral-100 text-neutral-800';
  }

  /**
   * Get the display label for a status
   */
  getStatusLabel(status: RefTestStatus): string {
    const labels: Record<RefTestStatus, string> = {
      [RefTestStatus.Pending]: 'Pending',
      [RefTestStatus.InProgress]: 'In Progress',
      [RefTestStatus.Completed]: 'Completed',
      [RefTestStatus.Expired]: 'Expired',
    };

    return labels[status] ?? 'Unknown';
  }

  /**
   * Format time in minutes to a readable string
   */
  formatTimeInMinutes(minutes: number): string {
    if (minutes < 60) {
      return `${minutes} min`;
    }

    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;

    if (remainingMinutes === 0) {
      return `${hours} hr`;
    }

    return `${hours} hr ${remainingMinutes} min`;
  }

  /**
   * Calculate percentage with optional decimal places
   */
  calculatePercentage(value: number, total: number, decimals: number = 0): number {
    if (total === 0) return 0;
    const percentage = (value / total) * 100;
    return Number(percentage.toFixed(decimals));
  }

  /**
   * Determine if a score meets the passing threshold
   */
  isPassing(score: number, totalQuestions: number, passingPercentage: number): boolean {
    const percentage = this.calculatePercentage(score, totalQuestions);
    return percentage >= passingPercentage;
  }

  /**
   * Get CSS class for score display based on passing status
   */
  getScoreClass(score: number, totalQuestions: number, passingPercentage: number): string {
    return this.isPassing(score, totalQuestions, passingPercentage)
      ? 'text-success-600'
      : 'text-red-600';
  }
}
