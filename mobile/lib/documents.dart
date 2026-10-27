import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import 'api_service.dart';
import 'currency.dart';

/// Document branding comes from the order/shift's outlet, never the operator's
/// cached profile or a different outlet selected by a Super Admin.
class OutletBrand {
  final String name, address, phone;
  const OutletBrand({
    required this.name,
    required this.address,
    required this.phone,
  });
  factory OutletBrand.fromDocument(Map<String, dynamic> data) =>
      OutletBrand._checked(
        data['outletName'],
        data['outletAddress'],
        data['outletPhone'],
      );
  factory OutletBrand.fromOutlet(Map<String, dynamic> data) =>
      OutletBrand._checked(data['name'], data['address'], data['phone']);
  factory OutletBrand._checked(dynamic name, dynamic address, dynamic phone) {
    final title = (name as String?)?.trim() ?? '';
    if (title.isEmpty) {
      throw const ApiException(
        'Outlet details are unavailable. Refresh before printing.',
      );
    }
    return OutletBrand(
      name: title,
      address: (address as String?)?.trim() ?? '',
      phone: (phone as String?)?.trim() ?? '',
    );
  }
  String get addressLine =>
      address.isEmpty ? 'Address not configured' : address;
  String get contactLine =>
      'Contact: ${phone.isEmpty ? 'Not configured' : phone}';
  String get filename => name.replaceAll(RegExp(r'[\\/:*?"<>|\x00-\x1f]'), '_');
}

class OutletBusinessHeader extends StatelessWidget {
  final OutletBrand outlet;
  const OutletBusinessHeader({super.key, required this.outlet});
  @override
  Widget build(BuildContext context) => SizedBox(
    width: double.infinity,
    child: Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Text(
          outlet.name,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: 8),
        Text(
          outlet.addressLine,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 13),
        ),
        const SizedBox(height: 4),
        Text(
          outlet.contactLine,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 13),
        ),
      ],
    ),
  );
}

pw.Widget pdfOutletHeader(OutletBrand outlet) => pw.Container(
  width: double.infinity,
  alignment: pw.Alignment.center,
  padding: const pw.EdgeInsets.only(bottom: 16),
  decoration: const pw.BoxDecoration(
    border: pw.Border(bottom: pw.BorderSide(width: .5)),
  ),
  child: pw.Column(
    mainAxisSize: pw.MainAxisSize.min,
    crossAxisAlignment: pw.CrossAxisAlignment.center,
    children: [
      pw.Text(
        outlet.name,
        textAlign: pw.TextAlign.center,
        style: pw.TextStyle(fontSize: 24, fontWeight: pw.FontWeight.bold),
      ),
      pw.SizedBox(height: 8),
      pw.Text(
        outlet.addressLine,
        textAlign: pw.TextAlign.center,
        style: const pw.TextStyle(fontSize: 11),
      ),
      pw.SizedBox(height: 4),
      pw.Text(
        outlet.contactLine,
        textAlign: pw.TextAlign.center,
        style: const pw.TextStyle(fontSize: 11),
      ),
    ],
  ),
);
double _number(dynamic value) => (value as num?)?.toDouble() ?? 0;
String _inr(dynamic value) => formatInr(_number(value)).replaceAll('₹', 'INR ');
String _date(dynamic value) {
  final d = DateTime.tryParse('$value')?.toLocal();
  return d == null
      ? '-'
      : '${d.day}/${d.month}/${d.year} ${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
}

pw.Document _document(OutletBrand outlet, String title) => pw.Document(
  title: '${outlet.name} - $title',
  author: outlet.name,
  creator: outlet.name,
);

Future<Uint8List> buildReceiptPdf(Map<String, dynamic> receipt) async {
  final outlet = OutletBrand.fromDocument(receipt);
  final doc = _document(outlet, 'Receipt ${receipt['orderNumber']}');
  doc.addPage(
    pw.MultiPage(
      header: (_) => pdfOutletHeader(outlet),
      build: (_) => [
        pw.SizedBox(height: 16),
        pw.Text('TRANSACTION RECEIPT - ${receipt['orderNumber']}'),
        pw.Text(
          '${receipt['serviceType']} | Table ${receipt['tableNumber'] ?? '-'} | ${receipt['status']}',
        ),
        pw.Text(_date(receipt['createdAt'])),
        pw.Divider(),
        ...((receipt['orderItems'] as List?) ?? []).map(
          (i) => pw.Padding(
            padding: const pw.EdgeInsets.symmetric(vertical: 4),
            child: pw.Row(
              mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
              children: [
                pw.Expanded(
                  child: pw.Text('${i['quantity']} x ${i['productName']}'),
                ),
                pw.Text(_inr(i['lineTotal'])),
              ],
            ),
          ),
        ),
        pw.Divider(),
        ...{
          'Subtotal': receipt['subtotal'],
          'Discount': receipt['discountAmount'],
          'GST': receipt['taxAmount'],
          'Total': receipt['totalAmount'],
        }.entries.map(
          (e) => pw.Row(
            mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
            children: [pw.Text(e.key), pw.Text(_inr(e.value))],
          ),
        ),
        pw.SizedBox(height: 16),
        pw.Text('Thank you. Keep this receipt for your records.'),
      ],
    ),
  );
  return doc.save();
}

Future<Uint8List> buildSalesPdf(OutletBrand outlet, List<dynamic> rows) async {
  final doc = _document(outlet, 'Sales report INR');
  doc.addPage(
    pw.MultiPage(
      header: (_) => pdfOutletHeader(outlet),
      build: (_) => [
        pw.SizedBox(height: 16),
        pw.Text('Sales report (INR)', style: const pw.TextStyle(fontSize: 18)),
        pw.SizedBox(height: 12),
        pw.TableHelper.fromTextArray(
          headers: [
            'Group',
            'Orders',
            'Gross',
            'Discount',
            'Tax',
            'Net',
            'Cash',
            'Card',
            'UPI',
          ],
          data: rows
              .map(
                (r) => [
                  '${r['groupKey']}',
                  '${r['orderCount']}',
                  ...[
                    'grossSales',
                    'discounts',
                    'taxes',
                    'netSales',
                    'cash',
                    'card',
                    'digitalWallet',
                  ].map((k) => _number(r[k]).toStringAsFixed(2)),
                ],
              )
              .toList(),
        ),
      ],
    ),
  );
  return doc.save();
}

Future<Uint8List> buildZReportPdf(Map<String, dynamic> report) async {
  final outlet = OutletBrand.fromDocument(report);
  final doc = _document(outlet, 'Z-report ${report['shiftId']}');
  doc.addPage(
    pw.MultiPage(
      header: (_) => pdfOutletHeader(outlet),
      build: (_) => [
        pw.SizedBox(height: 16),
        pw.Text(
          'Z-REPORT - SHIFT ${report['shiftId']}',
          style: pw.TextStyle(fontSize: 18, fontWeight: pw.FontWeight.bold),
        ),
        pw.Text('Cashier: ${report['userName']}'),
        pw.Text('Opened: ${_date(report['openTime'])}'),
        pw.Text('Closed: ${_date(report['closeTime'])}'),
        pw.SizedBox(height: 12),
        ...{
          'Gross sales': report['grossSales'],
          'Discounts': report['discounts'],
          'GST': report['taxes'],
          'Net sales': report['netSales'],
          'Opening float': report['openingFloat'],
          'Expected cash': report['cashExpected'],
          'Counted cash': report['cashCounted'],
          'Variance': report['variance'],
        }.entries.map(
          (e) => pw.Padding(
            padding: const pw.EdgeInsets.symmetric(vertical: 4),
            child: pw.Row(
              mainAxisAlignment: pw.MainAxisAlignment.spaceBetween,
              children: [pw.Text(e.key), pw.Text(_inr(e.value))],
            ),
          ),
        ),
      ],
    ),
  );
  return doc.save();
}

Future<Uint8List> buildOutletQrPdf(OutletBrand outlet, String pngBase64) async {
  final doc = _document(outlet, 'Ordering QR');
  doc.addPage(
    pw.Page(
      build: (_) => pw.Center(
        child: pw.Column(
          mainAxisSize: pw.MainAxisSize.min,
          children: [
            pdfOutletHeader(outlet),
            pw.SizedBox(height: 24),
            pw.Image(
              pw.MemoryImage(base64Decode(pngBase64)),
              width: 270,
              height: 270,
            ),
            pw.SizedBox(height: 20),
            pw.Text(
              'Scan to order - Pay cash at the outlet',
              textAlign: pw.TextAlign.center,
              style: const pw.TextStyle(fontSize: 18),
            ),
          ],
        ),
      ),
    ),
  );
  return doc.save();
}

Future<void> printReceipt(ApiService api, Map<String, dynamic> order) async {
  final receipt = Map<String, dynamic>.from(
    await api.request('/orders/${order['id']}/receipt', method: 'POST'),
  );
  final brand = OutletBrand.fromDocument(receipt);
  await Printing.layoutPdf(
    name: '${brand.filename} - Receipt ${receipt['orderNumber']}',
    onLayout: (_) => buildReceiptPdf(receipt),
  );
}

Future<void> printSales(
  ApiService api,
  List<dynamic> rows, {
  required int outletId,
}) async {
  final outlet = Map<String, dynamic>.from(
    await api.request('/outlets/$outletId'),
  );
  final brand = OutletBrand.fromOutlet(outlet);
  await Printing.layoutPdf(
    name: '${brand.filename} - Sales report INR',
    onLayout: (_) => buildSalesPdf(brand, rows),
  );
}

Future<void> printZReport(Map<String, dynamic> report) async {
  final brand = OutletBrand.fromDocument(report);
  await Printing.layoutPdf(
    name: '${brand.filename} - Z-report ${report['shiftId']}',
    onLayout: (_) => buildZReportPdf(report),
  );
}

Future<void> printOutletQr(
  ApiService api,
  Map<String, dynamic> outlet,
  String pngBase64,
) async {
  final current = Map<String, dynamic>.from(
    await api.request('/outlets/${outlet['id']}'),
  );
  final brand = OutletBrand.fromOutlet(current);
  await Printing.layoutPdf(
    name: '${brand.filename} - Ordering QR',
    onLayout: (_) => buildOutletQrPdf(brand, pngBase64),
  );
}
