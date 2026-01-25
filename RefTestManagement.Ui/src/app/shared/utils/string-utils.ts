/**
 * Converts a PascalCase string to snake_case
 * @param value The PascalCase string to convert
 * @returns The snake_case version of the string
 * @example
 * toSnakeCase('RefTestNotFoundError') // returns 'ref_test_not_found_error'
 */
export function toSnakeCase(value: string): string {
  return value
    .replace(/([A-Z])/g, '_$1')
    .toLowerCase()
    .replace(/^_/, '');
}
