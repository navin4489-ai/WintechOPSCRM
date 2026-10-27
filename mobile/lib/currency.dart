/// Indian Rupee (₹) currency formatting with lakh/crore digit grouping.
///
/// Mirrors the web app's `InrPipe` so both platforms render identically:
///
///     formatInr(1234.5)     // ₹1,234.50
///     formatInr(123456)     // ₹1,23,456.00
///     formatInr(12345678.9) // ₹1,23,45,678.90
///     formatInr(1234.5, decimals: 0)          // ₹1,235
///     formatInr(1234.5, symbol: false)        // 1,234.50
String formatInr(num? value, {int decimals = 2, bool symbol = true}) {
  if (value == null || value.isNaN) return symbol ? '₹0.00' : '0.00';

  final negative = value < 0;
  final abs = value.abs().toStringAsFixed(decimals);
  final parts = abs.split('.');
  final grouped = _groupIndian(parts[0]);
  final out = parts.length > 1 ? '$grouped.${parts[1]}' : grouped;

  final sign = negative ? '-' : '';
  final prefix = symbol ? '₹' : '';
  return '$sign$prefix$out';
}

/// Indian numbering system: last three digits, then groups of two
/// (crore / lakh / thousand / hundred).
String _groupIndian(String digits) {
  if (digits.length <= 3) return digits;

  final last3 = digits.substring(digits.length - 3);
  var rest = digits.substring(0, digits.length - 3);

  final parts = <String>[];
  while (rest.length > 2) {
    parts.insert(0, rest.substring(rest.length - 2));
    rest = rest.substring(0, rest.length - 2);
  }
  if (rest.isNotEmpty) parts.insert(0, rest);
  parts.add(last3);

  return parts.join(',');
}
