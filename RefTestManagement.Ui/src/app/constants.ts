/**
 * Application-wide constants
 * Single source of truth for magic numbers and configuration values
 */

/**
 * Toast notification durations in milliseconds
 */
export const TOAST_DURATION = {
  /** Error messages - longer display time */
  ERROR: 5000,
  /** Warning messages */
  WARNING: 5000,
  /** Success messages - shorter display */
  SUCCESS: 3000,
  /** Info messages - shorter display */
  INFO: 3000,
} as const;
