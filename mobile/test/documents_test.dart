import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:oro_pos_app/documents.dart';
import 'package:oro_pos_app/api_service.dart';

const receipt = {
  'id': 42,
  'outletId': 20,
  'outletName': 'Tenant B Restaurant',
  'outletAddress': '22 Market Road\nBengaluru 560001',
  'outletPhone': '+91 88888 88888',
  'orderNumber': 'BILL-0042',
  'status': 'Closed',
  'serviceType': 'Takeaway',
  'createdAt': '2026-10-27T12:00:00Z',
  'subtotal': 600,
  'discountAmount': 60,
  'taxAmount': 27,
  'totalAmount': 567,
  'orderItems': [
    {'quantity': 1, 'productName': 'Paneer Tikka', 'lineTotal': 280},
    {'quantity': 1, 'productName': 'Chicken Tikka', 'lineTotal': 320},
  ],
};
void main() {
  test('Document header uses authoritative outlet fields', () {
    final brand = OutletBrand.fromDocument(receipt);
    expect(brand.name, 'Tenant B Restaurant');
    expect(brand.address, '22 Market Road\nBengaluru 560001');
    expect(brand.contactLine, 'Contact: +91 88888 88888');
  });
  test('Missing outlet metadata cannot silently print the operator outlet or platform brand', () {
    expect(
      () => OutletBrand.fromDocument({'outletId': 20}),
      throwsA(isA<ApiException>()),
    );
  });
  testWidgets('Outlet name is bold and centered above address and contact', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: OutletBusinessHeader(outlet: OutletBrand.fromDocument(receipt)),
        ),
      ),
    );
    final title = tester.widget<Text>(find.text('Tenant B Restaurant'));
    expect(title.textAlign, TextAlign.center);
    expect(title.style?.fontWeight, FontWeight.w800);
    expect(
      tester.getTopLeft(find.text('Tenant B Restaurant')).dy,
      lessThan(
        tester.getTopLeft(find.text('22 Market Road\nBengaluru 560001')).dy,
      ),
    );
    expect(find.text('Contact: +91 88888 88888'), findsOneWidget);
    expect(find.textContaining('Wintech'), findsNothing);
    expect(find.textContaining('ORO'), findsNothing);
  });
  test('Receipt, sales, shift and URL-free QR PDF builders generate outlet-only documents', () async {
    final brand = OutletBrand.fromDocument(receipt);
    final report = {
      ...receipt,
      'shiftId': 7,
      'userName': 'cashier',
      'openTime': '2026-10-27T10:00:00Z',
      'closeTime': '2026-10-27T15:00:00Z',
      'grossSales': 600,
      'discounts': 60,
      'taxes': 27,
      'netSales': 540,
      'openingFloat': 500,
      'cashExpected': 1067,
      'cashCounted': 1067,
      'variance': 0,
    };
    const png =
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC';
    final files = {
      'sample-receipt.pdf': await buildReceiptPdf(receipt),
      'sample-sales.pdf': await buildSalesPdf(brand, [
        {
          'groupKey': 'Today',
          'orderCount': 1,
          'grossSales': 600,
          'discounts': 60,
          'taxes': 27,
          'netSales': 540,
          'cash': 567,
          'card': 0,
          'digitalWallet': 0,
        },
      ]),
      'sample-z-report.pdf': await buildZReportPdf(report),
      'sample-qr-poster.pdf': await buildOutletQrPdf(brand, png),
    };
    final directory = Platform.environment['WINTECH_PDF_PREVIEW_DIR'];
    for (final entry in files.entries) {
      expect(ascii.decode(entry.value.take(5).toList()), '%PDF-');
      final raw = latin1.decode(entry.value);
      expect(raw, isNot(contains('ORO POS')));
      expect(raw, isNot(contains('Powered by Wintech')));
      if (directory != null) {
        await Directory(directory).create(recursive: true);
        await File('$directory/${entry.key}').writeAsBytes(entry.value);
      }
    }
  });
}
