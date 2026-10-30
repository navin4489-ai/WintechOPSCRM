import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:oro_pos_app/api_service.dart';
import 'package:oro_pos_app/main.dart';
import 'package:oro_pos_app/models.dart';
import 'package:oro_pos_app/pos_screen.dart';
import 'package:oro_pos_app/currency.dart';
import 'package:oro_pos_app/ui_helpers.dart';

class FakeApi extends ApiService {
  bool signedIn = false;
  final List<String> calls = [];
  final List<Object?> bodies = [];
  @override
  bool get isAuthenticated => signedIn;
  @override
  User? get user => signedIn
      ? User(
          id: 1,
          username: 'operator',
          roleName: 'Owner',
          permissions: [],
          outletId: 7,
          outletName: 'Test outlet',
        )
      : null;
  @override
  Future<bool> login(String username, String password) async {
    signedIn = true;
    return true;
  }

  @override
  Future<dynamic> request(
    String path, {
    String method = 'GET',
    Object? body,
  }) async {
    calls.add('$method $path');
    bodies.add(body);
    if (path.startsWith('/tables/outlet/')) {
      return [
        {
          'id': 42,
          'tableNumber': 'A1',
          'status': 'Occupied',
          'capacity': 4,
          'hasOpenOrder': true,
        },
      ];
    }
    if (path.startsWith('/products')) {
      return [
        {
          'id': 3,
          'name': 'Paneer',
          'sku': 'P1',
          'price': 100,
          'categoryName': 'Main',
        },
      ];
    }
    if (path == '/orders/table/42/open') {
      return {
        'id': 8,
        'orderNumber': 'QA-8',
        'serviceType': 'DineIn',
        'status': 'Open',
        'subtotal': 100,
        'taxAmount': 5,
        'totalAmount': 105,
        'orderItems': [
          {'id': 5, 'productName': 'Paneer', 'quantity': 1, 'lineTotal': 100},
        ],
        'payments': [],
      };
    }
    if (path == '/orders/8/payment') {
      return {
        'id': 8,
        'orderNumber': 'QA-8',
        'serviceType': 'DineIn',
        'status': 'Closed',
        'subtotal': 100,
        'taxAmount': 5,
        'totalAmount': 105,
        'orderItems': [],
        'payments': [
          {'status': 'Captured', 'amount': 105},
        ],
      };
    }
    if (path.startsWith('/reports/dashboard')) {
      return {
        'todaySales': 105,
        'todayCash': 105,
        'todayOrders': 1,
        'openTables': 1,
        'pendingKitchenTickets': 0,
        'lowStockItems': 0,
      };
    }
    if (path.startsWith('/shifts/open')) return null;
    return [];
  }
}

class FakeOutletAdmin extends FakeApi {
  @override
  User? get user => User(
    id: 2,
    username: 'outlet_admin',
    roleName: 'StoreManager',
    permissions: [
      'CreateOrder',
      'EditOrder',
      'ViewReports',
      'ManageInventory',
      'ManageUsers',
    ],
    outletId: 7,
    outletName: 'Only my outlet',
  );
  @override
  Future<dynamic> request(
    String path, {
    String method = 'GET',
    Object? body,
  }) async {
    if (path == '/outlet-settings') {
      calls.add('$method $path');
      bodies.add(body);
      return {
        'id': 7,
        'name': 'Only my outlet',
        'currency': 'INR',
        'timezone': 'Asia/Kolkata',
        'isOperating': true,
      };
    }
    return super.request(path, method: method, body: body);
  }
}

void main() {
  test('Outlet admin cannot switch to another outlet workspace', () async {
    final api = FakeOutletAdmin()..signedIn = true;
    await expectLater(
      api.selectOutlet(99, 'Another outlet'),
      throwsA(isA<ApiException>()),
    );
    expect(api.outletId, 7);
    expect(api.canManageCatalogue, isTrue);
    expect(api.can('ManageSettings'), isFalse);
  });
  testWidgets('Outlet admin sees own settings but not Super Admin navigation', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final api = FakeOutletAdmin()..signedIn = true;
    await tester.pumpWidget(MyApp(api: api));
    await tester.pumpAndSettle();
    await tester.tap(find.byIcon(Icons.menu));
    await tester.pumpAndSettle();
    expect(find.text('Super Admin'), findsNothing);
    await tester.scrollUntilVisible(
      find.text('My Outlet'),
      150,
      scrollable: find
          .descendant(
            of: find.byType(Drawer),
            matching: find.byType(Scrollable),
          )
          .first,
    );
    await tester.tap(find.text('My Outlet'));
    await tester.pumpAndSettle();
    expect(api.calls, contains('GET /outlet-settings'));
    expect(api.calls.any((c) => c.contains('/super-admin/')), isFalse);
    expect(tester.takeException(), isNull);
  });
  test('INR formatter groups lakhs and crores', () {
    expect(formatInr(123456.78), '₹1,23,456.78');
    expect(formatInr(12345678.9), '₹1,23,45,678.90');
  });
  testWidgets('Sign-in has branding and no default credentials', (
    tester,
  ) async {
    await tester.pumpWidget(MyApp(api: FakeApi()));
    expect(find.text('Wintech POS'), findsOneWidget);
    expect(find.text('Sign in securely'), findsOneWidget);
    expect(find.text('Username or email'), findsOneWidget);
    expect(find.text('Admin@123'), findsNothing);
    expect(find.text('admin'), findsNothing);
    await tester.tap(find.text('Sign in securely'));
    await tester.pumpAndSettle();
    expect(find.text('Enter your username or email'), findsOneWidget);
    expect(find.text('Enter your password'), findsOneWidget);
  });
  testWidgets('Phone sign-in fits without layout overflow', (tester) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(MyApp(api: FakeApi()));
    expect(tester.takeException(), isNull);
  });
  testWidgets('Authenticated dashboard uses assigned outlet', (tester) async {
    final api = FakeApi()..signedIn = true;
    await tester.pumpWidget(MyApp(api: api));
    await tester.pumpAndSettle();
    expect(find.text('Hello, operator'), findsOneWidget);
    expect(api.calls.any((x) => x.contains('outletId=7')), isTrue);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Occupied table resumes order instead of creating duplicates', (
    tester,
  ) async {
    final api = FakeApi()..signedIn = true;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: PosScreen(api: api)),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Table A1'));
    await tester.pumpAndSettle();
    expect(api.calls, contains('GET /orders/table/42/open'));
    expect(api.calls.any((c) => c == 'POST /orders'), isFalse);
    expect(find.textContaining('QA-8'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  testWidgets('Required reason form cannot submit empty values', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: TextButton(
              onPressed: () => askForm(context, 'Void', [
                const FormFieldSpec('reason', 'Reason'),
              ]),
              child: const Text('Open'),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();
    expect(find.text('Reason is required'), findsOneWidget);
  });
  testWidgets('Payment selector sends the chosen UPI tender', (tester) async {
    final api = FakeApi()..signedIn = true;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: PosScreen(
            api: api,
            initialOrder: {
              'id': 8,
              'orderNumber': 'QA-8',
              'serviceType': 'DineIn',
              'status': 'Open',
              'subtotal': 100,
              'taxAmount': 5,
              'totalAmount': 105,
              'orderItems': [
                {
                  'id': 5,
                  'productName': 'Paneer',
                  'quantity': 1,
                  'lineTotal': 100,
                },
              ],
              'payments': [],
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final pay = find.text('Take payment · ₹105.00');
    await tester.ensureVisible(pay);
    await tester.tap(pay);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cash').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('UPI (externally confirmed)').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Record payment'));
    await tester.pumpAndSettle();
    final index = api.calls.indexOf('POST /orders/8/payment');
    expect(index, greaterThanOrEqualTo(0));
    expect((api.bodies[index] as Map)['tenderType'], 'UPI');
    expect((api.bodies[index] as Map)['amount'], 105);
    expect(tester.takeException(), isNull);
  });
}
