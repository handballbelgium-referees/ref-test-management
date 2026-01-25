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
  /** Success messages - longer display to allow for page navigation */
  SUCCESS: 6000,
  /** Info messages - shorter display */
  INFO: 3000,
} as const;
