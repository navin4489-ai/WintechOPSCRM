import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formats a number as Indian Rupees with proper lakh/crore digit grouping.
 *
 *   1234.5      -> ₹1,234.50
 *   123456      -> ₹1,23,456.00
 *   12345678.9  -> ₹1,23,45,678.90
 *
 * Usage: {{ amount | inr }}            (2 decimals, symbol)
 *        {{ amount | inr:0 }}          (0 decimals)
 *        {{ amount | inr:2:false }}    (hide symbol)
 */
@Pipe({ name: 'inr', standalone: true })
export class InrPipe implements PipeTransform {
  transform(value: number | string | null | undefined, decimals: number = 2, showSymbol: boolean = true): string {
    if (value === null || value === undefined || value === '') return showSymbol ? '₹0.00' : '0.00';

    const num = typeof value === 'string' ? parseFloat(value) : value;
    if (isNaN(num)) return showSymbol ? '₹0.00' : '0.00';

    const negative = num < 0;
    const abs = Math.abs(num).toFixed(decimals);
    const [intPart, decPart] = abs.split('.');

    const grouped = this.groupIndian(intPart);
    const result = decPart ? `${grouped}.${decPart}` : grouped;
    const sign = negative ? '-' : '';
    const symbol = showSymbol ? '₹' : '';
    return `${sign}${symbol}${result}`;
  }

  /** Indian numbering: last 3 digits, then groups of 2 (crore/lakh/thousand). */
  private groupIndian(digits: string): string {
    if (digits.length <= 3) return digits;
    const last3 = digits.slice(-3);
    let rest = digits.slice(0, -3);
    const parts: string[] = [];
    while (rest.length > 2) {
      parts.unshift(rest.slice(-2));
      rest = rest.slice(0, -2);
    }
    if (rest.length) parts.unshift(rest);
    return [...parts, last3].join(',');
  }
}
