import 'dart:async';
import 'dart:convert';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import 'api_service.dart';
import 'brand.dart';
import 'currency.dart';
import 'ui_helpers.dart';
import 'pos_screen.dart';

class OperationsScreen extends StatefulWidget {
  final ApiService api;
  final String module;
  final void Function(String) onNavigate;
  const OperationsScreen({
    super.key,
    required this.api,
    required this.module,
    required this.onNavigate,
  });
  @override
  State<OperationsScreen> createState() => _OperationsScreenState();
}

class _OperationsScreenState extends State<OperationsScreen> {
  Timer? kitchenPoll;
  bool fetching = false;
  bool loading = true, busy = false;
  String? error;
  dynamic data;
  dynamic shift;
  List<dynamic> rows = [];
  int page = 1;
  int? loadedOutletId;
  String filter = '', reportQuery = '';
  ApiService get api => widget.api;
  String get module => widget.module;
  @override
  void initState() {
    super.initState();
    // The ringing itself is owned by the app-wide KitchenAlertService in
    // main.dart; here we only keep the ticket list on screen fresh.
    load();
    if (module == 'Kitchen') {
      kitchenPoll = Timer.periodic(const Duration(seconds: 5), (_) {
        if (!loading && !busy && mounted) load(silent: true);
      });
    }
  }

  @override
  void dispose() {
    kitchenPoll?.cancel();
    super.dispose();
  }

  Future<void> load({bool silent = false}) async {
    if (fetching) return;
    fetching = true;
    setState(() {
      if (!silent) loading = true;
      error = null;
    });
    try {
      final outlet = module == 'Super Admin' ? null : api.outletId;
      final result = await api.request(switch (module) {
        'Overview' =>
          api.can('ViewReports')
              ? '/reports/dashboard?outletId=$outlet'
              : '/tables/outlet/$outlet',
        'Orders' =>
          '/orders?outletId=$outlet&page=$page&pageSize=20&search=${Uri.encodeQueryComponent(filter)}',
        'Kitchen' => '/kitchentickets?outletId=$outlet',
        'Shifts' => '/shifts?outletId=$outlet&page=$page&pageSize=20',
        'Tables' => '/tables/outlet/$outlet',
        'Catalogue' => '/products?outletId=$outlet&availableOnly=false',
        'Inventory' => '/inventory/stock?outletId=$outlet',
        'Reports' => '/reports/sales?outletId=$outlet$reportQuery',
        'Audit log' => '/reports/audit?outletId=$outlet&page=$page&pageSize=20',
        'Staff' => '/users',
        'Super Admin' => '/super-admin/outlets',
        'My Outlet' => '/outlet-settings',
        _ => throw const ApiException('Unknown module'),
      });
      dynamic open;
      if (module == 'Shifts' || module == 'Overview') {
        open = await api.request('/shifts/open?outletId=$outlet');
      }
      if (mounted) {
        setState(() {
          data = result;
          loadedOutletId = outlet;
          shift = open;
          rows = module == 'My Outlet' && result is Map
              ? [result]
              : result is List
              ? result
              : result is Map && result['items'] is List
              ? result['items']
              : [];
        });
      }
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      fetching = false;
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> run(Future<void> Function() task) async {
    if (busy) return;
    setState(() => busy = true);
    try {
      await task();
      await load();
    } catch (e) {
      if (mounted) notify(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> openShift() async {
    final value = await askForm(context, 'Open cash shift', [
      const FormFieldSpec(
        'openingFloat',
        'Opening cash float (INR)',
        number: true,
        initial: '0',
      ),
    ]);
    if (value == null) return;
    if (money(value['openingFloat']) < 0) {
      if (mounted) notify(context, 'Float cannot be negative.');
      return;
    }
    await run(() async {
      await api.request(
        '/shifts/open',
        method: 'POST',
        body: {...value, 'userId': api.user!.id, 'outletId': api.outletId},
      );
    });
  }

  Future<void> closeShift() async {
    final value = await askForm(
      context,
      'Close shift',
      [
        const FormFieldSpec(
          'countedCash',
          'Counted cash in drawer (INR)',
          number: true,
        ),
      ],
      warning: 'Count physical cash first. Cash variance will be stored in the Z-report.',
      submit: 'Close shift',
    );
    if (value == null) return;
    await run(() async {
      final result = await api.request(
        '/shifts/close',
        method: 'POST',
        body: {...value, 'shiftId': shift['id']},
      );
      if (mounted) {
        notify(
          context,
          'Shift closed · variance ${formatInr(money(result['variance']))}',
        );
      }
    });
  }

  Future<void> zReport(dynamic id) async {
    await run(() async {
      final report = await api.request('/shifts/$id/z-report');
      if (!mounted) return;
      await showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          title: report['outletName'] != null
              ? OutletBusinessHeader(
                  outlet: OutletBrand.fromDocument(
                    Map<String, dynamic>.from(report),
                  ),
                )
              : Text('Z-report · shift $id'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '${report['userName']} · ${dateLabel(report['openTime'])}',
                ),
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
                  (e) => ListTile(
                    dense: true,
                    title: Text(e.key),
                    trailing: Text(
                      formatInr(money(e.value)),
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () async {
                try {
                  await printZReport(Map<String, dynamic>.from(report));
                } catch (e) {
                  if (ctx.mounted) notify(ctx, e);
                }
              },
              child: const Text('Print / PDF'),
            ),
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Done'),
            ),
          ],
        ),
      );
    });
  }

  Future<void> inventoryMovement() async {
    final values = await askForm(context, 'Record stock movement', [
      FormFieldSpec(
        'productId',
        'Product',
        choices: {
          for (final p in rows) '${p['productId']}': '${p['productName']}',
        },
      ),
      const FormFieldSpec(
        'movementType',
        'Movement',
        initial: 'Receipt',
        choices: {
          'Receipt': 'Receipt / stock in',
          'Waste': 'Waste / spoilage',
          'Adjustment': 'Signed adjustment (+ / −)',
          'PhysicalCount': 'Physical count (actual stock on hand)',
        },
      ),
      const FormFieldSpec('quantity', 'Quantity', number: true),
      const FormFieldSpec('location', 'Location', initial: 'Main store'),
      const FormFieldSpec('batch', 'Batch / lot', optional: true),
      const FormFieldSpec('source', 'Reference / reason'),
    ]);
    if (values == null) return;
    values['productId'] = int.parse(values['productId']);
    await run(() async {
      await api.request('/inventory/movement', method: 'POST', body: values);
    });
  }

  Future<void> inventoryHistory() async {
    await run(() async {
      final entries = await api.request(
        '/inventory/movements?outletId=${api.outletId}&pageSize=100',
      );
      if (!mounted) return;
      await showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Latest stock movements'),
          content: SizedBox(
            width: 520,
            height: 440,
            child: ListView(
              children: (entries as List)
                  .map(
                    (e) => ListTile(
                      title: Text('${e['productName']} · ${e['movementType']}'),
                      subtitle: Text(
                        '${dateLabel(e['createdAt'])}\n${e['source'] ?? ''}',
                      ),
                      trailing: Text('${e['quantity']}'),
                    ),
                  )
                  .toList(),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Done'),
            ),
          ],
        ),
      );
    });
  }

  Future<void> addRecipe() async {
    final choices = {
      for (final p in rows) '${p['productId']}': '${p['productName']}',
    };
    final values = await askForm(context, 'Recipe / ingredient mapping', [
      FormFieldSpec('productId', 'Menu product', choices: choices),
      FormFieldSpec('ingredientId', 'Ingredient product', choices: choices),
      const FormFieldSpec(
        'quantity',
        'Ingredient quantity per sale',
        number: true,
      ),
      const FormFieldSpec('unit', 'Unit', initial: 'unit'),
    ]);
    if (values == null) return;
    for (final k in ['productId', 'ingredientId']) {
      values[k] = int.parse(values[k]);
    }
    if (values['productId'] == values['ingredientId'] ||
        money(values['quantity']) <= 0) {
      if (mounted) {
        notify(
          context,
          'Choose a different ingredient and a positive quantity.',
        );
      }
      return;
    }
    await run(() async {
      await api.request('/recipes', method: 'POST', body: values);
    });
  }

  Future<void> addProduct([dynamic product]) async {
    await run(() async {
      final categories =
          await api.request('/categories/outlet/${api.outletId}') as List;
      if (!mounted) return;
      final value = await askForm(
        context,
        product == null ? 'Create product' : 'Edit item',
        [
          FormFieldSpec(
            'name',
            'Product name',
            initial: product?['name'] ?? '',
          ),
          FormFieldSpec('sku', 'Unique SKU', initial: product?['sku'] ?? ''),
          FormFieldSpec(
            'barcode',
            'Barcode',
            optional: true,
            initial: product?['barcode'] ?? '',
          ),
          FormFieldSpec(
            'categoryId',
            'Category',
            initial: '${product?['categoryId'] ?? ''}',
            choices: {for (final c in categories) '${c['id']}': '${c['name']}'},
          ),
          FormFieldSpec(
            'price',
            'Price (INR)',
            number: true,
            initial: '${product?['price'] ?? ''}',
          ),
          FormFieldSpec(
            'description',
            'Description',
            optional: true,
            initial: product?['description'] ?? '',
          ),
          FormFieldSpec(
            'available',
            'Availability',
            initial: product?['isAvailable'] == false ? 'no' : 'yes',
            choices: const {
              'yes': 'Available on POS and public menu',
              'no': 'Unavailable',
            },
          ),
        ],
      );
      if (value == null) return;
      value['categoryId'] = int.parse(value['categoryId']);
      await api.request(
        product == null ? '/products' : '/products/${product['id']}',
        method: product == null ? 'POST' : 'PUT',
        body: {
          ...value,
          'outletId': api.outletId,
          'isAvailable': value.remove('available') == 'yes',
          'taxClass': 'Standard',
          'sortOrder': 0,
        },
      );
    });
  }

  Future<void> productImage(dynamic product) async {
    try {
      final file = await ImagePicker().pickImage(
        source: ImageSource.gallery,
        maxWidth: 1200,
        maxHeight: 1200,
        imageQuality: 80,
      );
      if (file == null) return;
      final bytes = await file.readAsBytes();
      await run(() => api.uploadProductImage(product['id'], bytes));
    } catch (e) {
      if (mounted) notify(context, e);
    }
  }

  Future<void> outletForm([dynamic outlet]) async {
    if (outlet == null && api.user?.roleName != 'Owner') return;
    final random = Random.secure();
    const alphabet =
        'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#';
    final generated =
        'Wt2!${List.generate(16, (_) => alphabet[random.nextInt(alphabet.length)]).join()}';
    final value = await askForm(
      context,
      outlet == null ? 'Create outlet' : 'Outlet configuration',
      [
        FormFieldSpec('name', 'Outlet name', initial: outlet?['name'] ?? ''),
        FormFieldSpec(
          'address',
          'Address',
          initial: outlet?['address'] ?? '',
          optional: true,
        ),
        FormFieldSpec(
          'phone',
          'Phone',
          initial: outlet?['phone'] ?? '',
          optional: true,
        ),
        FormFieldSpec(
          'timezone',
          'Timezone',
          initial: outlet?['timezone'] ?? 'Asia/Kolkata',
        ),
        FormFieldSpec(
          'operating',
          'Outlet status',
          initial: outlet?['isOperating'] == false ? 'no' : 'yes',
          choices: const {'yes': 'Operating', 'no': 'Closed'},
        ),
        if (outlet == null) ...[
          FormFieldSpec(
            'adminUsername',
            'Outlet admin user ID',
            initial: 'outlet_admin_${random.nextInt(99999999)}',
          ),
          const FormFieldSpec(
            'adminEmail',
            'Admin email (optional, unique)',
            optional: true,
          ),
          FormFieldSpec(
            'adminPassword',
            'Initial admin password (12+ characters)',
            secret: true,
            initial: generated,
          ),
        ],
      ],
    );
    if (value == null) return;
    await run(() async {
      final details = {
        'name': value['name'],
        'address': value['address'],
        'phone': value['phone'],
        'timezone': value['timezone'],
        'id': outlet?['id'] ?? 0,
        'currency': 'INR',
        'isOperating': value['operating'] == 'yes',
        'taxSetup': outlet?['taxSetup'],
      };
      final result = await api.request(
        outlet == null
            ? '/super-admin/outlets'
            : api.user?.roleName == 'Owner'
            ? '/super-admin/outlets/${outlet['id']}'
            : '/outlet-settings',
        method: outlet == null ? 'POST' : 'PUT',
        body: outlet == null
            ? {
                'outlet': details,
                'adminUsername': value['adminUsername'],
                'adminPassword': value['adminPassword'],
                'adminEmail': value['adminEmail'] == ''
                    ? null
                    : value['adminEmail'],
              }
            : details,
      );
      if (outlet == null && mounted) {
        bool show = false;
        final login = '${Uri.parse(ApiService.baseUrl).origin}/login';
        final username = result['administrator']['username'];
        final password = value['adminPassword'];
        await showDialog(
          context: context,
          builder: (ctx) => StatefulBuilder(
            builder: (ctx, update) => AlertDialog(
              title: const Text('Outlet admin login created'),
              content: SizedBox(
                width: 430,
                child: SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        '${result['outlet']['name']}',
                        style: const TextStyle(fontWeight: FontWeight.w800),
                      ),
                      const SizedBox(height: 12),
                      SelectableText('Sign in: $login\nUser ID: $username'),
                      const SizedBox(height: 12),
                      const Text('Initial password'),
                      SelectableText(show ? '$password' : '••••••••••••••••'),
                      TextButton(
                        onPressed: () => update(() => show = !show),
                        child: Text(show ? 'Hide password' : 'Show password'),
                      ),
                      const Text(
                        'Share securely. This account can run only its assigned outlet. Passwords cannot be retrieved later; ask the admin to change it after signing in.',
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Clipboard.setData(
                    ClipboardData(
                      text:
                          'Wintech POS — ${result['outlet']['name']}\nSign in: $login\nUser ID: $username\nInitial password: $password\nAccess: assigned outlet only. Please change password after signing in.',
                    ),
                  ),
                  child: const Text('Copy login details'),
                ),
                FilledButton(
                  onPressed: () => Navigator.pop(ctx),
                  child: const Text('Done'),
                ),
              ],
            ),
          ),
        );
      }
    });
  }

  Future<void> outletQr(dynamic outlet) async {
    await run(() async {
      final orderingPath = api.user?.roleName == 'Owner'
          ? '/super-admin/outlets/${outlet['id']}/ordering'
          : '/outlet-settings/ordering';
      var config = Map<String, dynamic>.from(await api.request(orderingPath));
      if (!mounted) return;
      bool updating = false;
      await showDialog(
        context: context,
        builder: (ctx) => StatefulBuilder(
          builder: (ctx, update) => AlertDialog(
            title: OutletBusinessHeader(
              outlet: OutletBrand.fromOutlet(Map<String, dynamic>.from(outlet)),
            ),
            content: SizedBox(
              width: 430,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text(
                      'Customers scan this code, order without signing in and pay cash at the outlet. An open staff shift is required.',
                    ),
                    if (config['qrPngBase64'] != null)
                      Padding(
                        padding: const EdgeInsets.all(16),
                        child: Image.memory(
                          base64Decode(config['qrPngBase64']),
                          width: 230,
                          height: 230,
                        ),
                      ),
                    const SizedBox(height: 12),
                    Text(
                      config['enabled'] == true
                          ? 'Public ordering enabled'
                          : 'Public ordering disabled',
                    ),
                    if (updating) const LinearProgressIndicator(),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: updating ? null : () => Navigator.pop(ctx),
                child: const Text('Done'),
              ),
              if (config['publicUrl'] != null)
                TextButton(
                  onPressed: () => Clipboard.setData(
                    ClipboardData(text: config['publicUrl']),
                  ),
                  child: const Text('Copy link'),
                ),
              if (config['qrPngBase64'] != null)
                TextButton(
                  onPressed: () async {
                    try {
                      await printOutletQr(
                        api,
                        Map<String, dynamic>.from(outlet),
                        config['qrPngBase64'],
                      );
                    } catch (e) {
                      if (ctx.mounted) notify(ctx, e);
                    }
                  },
                  child: const Text('Print QR'),
                ),
              FilledButton(
                onPressed: updating
                    ? null
                    : () async {
                        update(() => updating = true);
                        try {
                          final result = await api.request(
                            orderingPath,
                            method: 'POST',
                            body: {
                              'enabled': config['enabled'] != true,
                              'rotateKey': false,
                            },
                          );
                          if (ctx.mounted) {
                            update(
                              () => config = Map<String, dynamic>.from(result),
                            );
                          }
                        } catch (e) {
                          if (ctx.mounted) notify(ctx, e);
                        } finally {
                          if (ctx.mounted) update(() => updating = false);
                        }
                      },
                child: Text(
                  config['enabled'] == true ? 'Disable' : 'Enable & generate',
                ),
              ),
            ],
          ),
        ),
      );
    });
  }

  Future<void> addCategory() async {
    final value = await askForm(context, 'Create category', [
      const FormFieldSpec('name', 'Category name'),
    ]);
    if (value != null) {
      await run(() async {
        await api.request(
          '/categories',
          method: 'POST',
          body: {...value, 'outletId': api.outletId, 'sortOrder': 0},
        );
      });
    }
  }

  Future<void> addTable() async {
    final value = await askForm(context, 'Create table', [
      const FormFieldSpec('tableNumber', 'Table number'),
      const FormFieldSpec('capacity', 'Seats', number: true, initial: '4'),
    ]);
    if (value != null) {
      await run(() async {
        await api.request(
          '/tables',
          method: 'POST',
          body: {...value, 'outletId': api.outletId},
        );
      });
    }
  }

  Future<void> staffForm([dynamic staff]) async {
    await run(() async {
      final roles = ((await api.request('/roles')) as List)
          .where(
            (r) =>
                api.user?.roleName == 'Owner' ||
                (r['name'] != 'Owner' &&
                    ((r['permissions'] as List?) ?? []).every(
                      (p) => api.user?.permissions.contains(p) == true,
                    )),
          )
          .toList();
      final outlets = await api.request('/outlets') as List;
      if (!mounted) return;
      final value = await askForm(
        context,
        staff == null ? 'Create staff account' : 'Edit staff account',
        [
          FormFieldSpec(
            'username',
            'Username',
            initial: staff?['username'] ?? '',
          ),
          FormFieldSpec('email', 'Email', initial: staff?['email'] ?? ''),
          FormFieldSpec(
            'password',
            staff == null
                ? 'Password (12+ characters)'
                : 'New password (leave blank to keep)',
            secret: true,
            optional: staff != null,
          ),
          FormFieldSpec(
            'roleId',
            'Role',
            initial:
                '${roles.where((r) => r['name'] == staff?['roleName']).firstOrNull?['id'] ?? ''}',
            choices: {for (final r in roles) '${r['id']}': '${r['name']}'},
          ),
          FormFieldSpec(
            'outletId',
            'Assigned outlet',
            initial: '${staff?['outletId'] ?? api.outletId}',
            choices: {for (final o in outlets) '${o['id']}': '${o['name']}'},
          ),
          if (staff != null)
            FormFieldSpec(
              'active',
              'Account status',
              initial: staff['isActive'] == true ? 'yes' : 'no',
              choices: const {'yes': 'Active', 'no': 'Inactive'},
            ),
        ],
      );
      if (value == null) return;
      if ((value['password'] as String).isNotEmpty &&
          (value['password'] as String).length < 12) {
        throw const ApiException('Use a password of at least 12 characters.');
      }
      if (value['password'] == '') value.remove('password');
      value['roleId'] = int.parse(value['roleId']);
      value['outletId'] = int.parse(value['outletId']);
      if (staff != null) value['isActive'] = value.remove('active') == 'yes';
      await api.request(
        staff == null ? '/users' : '/users/${staff['id']}',
        method: staff == null ? 'POST' : 'PUT',
        body: value,
      );
    });
  }

  Future<void> showOrder(dynamic summary) async {
    await run(() async {
      final detail = Map<String, dynamic>.from(
        await api.request('/orders/${summary['id']}'),
      );
      if (!mounted) return;
      if (detail['status'] == 'Open' && api.can('CreateOrder')) {
        await Navigator.push(
          context,
          MaterialPageRoute(
            builder: (_) => Scaffold(
              appBar: AppBar(title: Text('${detail['orderNumber']}')),
              body: PosScreen(api: api, initialOrder: detail),
            ),
          ),
        );
        return;
      }
      await showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          title: detail['outletName'] != null
              ? OutletBusinessHeader(outlet: OutletBrand.fromDocument(detail))
              : Text('${detail['orderNumber']}'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('${detail['orderNumber']}'),
                Text('${detail['status']} · ${dateLabel(detail['createdAt'])}'),
                ...((detail['orderItems'] as List?) ?? []).map(
                  (i) => ListTile(
                    title: Text('${i['quantity']} × ${i['productName']}'),
                    trailing: Text(formatInr(money(i['lineTotal']))),
                  ),
                ),
                Text(
                  'GST ${formatInr(money(detail['taxAmount']))} · Total ${formatInr(money(detail['totalAmount']))}',
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Done'),
            ),
            if (api.can('EditOrder') || api.can('ViewReports'))
              TextButton(
                onPressed: () async {
                  try {
                    await printReceipt(api, detail);
                  } catch (e) {
                    if (ctx.mounted) notify(ctx, e);
                  }
                },
                child: const Text('Print receipt'),
              ),
            if (detail['status'] == 'Closed' && api.can('RefundOrder'))
              TextButton(
                onPressed: () {
                  Navigator.pop(ctx);
                  refund(detail);
                },
                child: const Text('Record refund'),
              ),
          ],
        ),
      );
    });
  }

  Future<void> refund(Json detail) async {
    final value = await askForm(
      context,
      'Record full refund',
      [
        ...approvalFields(api),
        const FormFieldSpec('reason', 'Reason'),
        const FormFieldSpec('reference', 'Bank / manual refund reference'),
        const FormFieldSpec(
          'restock',
          'Return physical stock?',
          initial: 'no',
          choices: {'no': 'No (food / waste)', 'yes': 'Yes, goods returned'},
        ),
      ],
      warning: 'First return the money externally. This records the full refund; it does not send money through a bank. Cash refunds require an open shift.',
      submit: 'Record full refund',
    );
    if (value == null) return;
    value['restock'] = value['restock'] == 'yes';
    await run(() async {
      await api.request(
        '/orders/${detail['id']}/refund',
        method: 'POST',
        body: value,
      );
    });
  }

  Future<void> filterReport() async {
    final value = await askForm(context, 'Sales report filters', [
      const FormFieldSpec('from', 'From date (YYYY-MM-DD)', optional: true),
      const FormFieldSpec('to', 'To date (YYYY-MM-DD)', optional: true),
      const FormFieldSpec(
        'groupBy',
        'Group by',
        initial: 'day',
        choices: {'day': 'Day', 'employee': 'Employee', 'outlet': 'Outlet'},
      ),
    ]);
    if (value == null) return;
    final params = <String, String>{'groupBy': value['groupBy']};
    for (final pair in [('from', 'fromDate'), ('to', 'toDate')]) {
      if (value[pair.$1] == '') continue;
      final date = DateTime.tryParse(value[pair.$1]);
      if (date == null) {
        if (mounted) notify(context, 'Enter dates in YYYY-MM-DD format.');
        return;
      }
      params[pair.$2] =
          (pair.$1 == 'to'
                  ? date
                        .add(const Duration(days: 1))
                        .subtract(const Duration(microseconds: 1))
                  : date)
              .toUtc()
              .toIso8601String();
    }
    reportQuery = '&${Uri(queryParameters: params).query}';
    await load();
  }

  Widget overview() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Container(
        padding: const EdgeInsets.all(24),
        decoration: BoxDecoration(
          gradient: const LinearGradient(colors: [navy, Color(0xff254f7d)]),
          borderRadius: BorderRadius.circular(24),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const WintechLogo(size: 48),
            const SizedBox(height: 20),
            Text(
              'Hello, ${api.user?.username}',
              style: const TextStyle(
                color: Colors.white,
                fontSize: 28,
                fontWeight: FontWeight.w800,
              ),
            ),
            Text(
              '${api.user?.outletName ?? 'Your outlet'} · ${api.user?.roleName}',
              style: const TextStyle(color: Colors.white70),
            ),
            const SizedBox(height: 20),
            Text(
              shift == null
                  ? 'No open cash shift'
                  : 'Shift #${shift['id']} is active',
              style: const TextStyle(color: gold, fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 12),
            FilledButton.tonal(
              onPressed: () =>
                  widget.onNavigate(shift == null ? 'Shifts' : 'Point of sale'),
              child: Text(
                shift == null ? 'Open your shift' : 'Go to point of sale',
              ),
            ),
          ],
        ),
      ),
      const SizedBox(height: 20),
      if (data is Map)
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            metric(
              'Sales today',
              formatInr(money(data['todaySales'])),
              Icons.trending_up,
            ),
            metric(
              'Cash collected',
              formatInr(money(data['todayCash'])),
              Icons.payments_outlined,
            ),
            metric(
              'Orders today',
              '${data['todayOrders']}',
              Icons.receipt_long,
            ),
            metric(
              'Occupied tables',
              '${data['openTables']}',
              Icons.table_restaurant,
            ),
            metric(
              'Kitchen queue',
              '${data['pendingKitchenTickets']}',
              Icons.soup_kitchen,
            ),
            metric(
              'Low stock',
              '${data['lowStockItems']}',
              Icons.inventory_2_outlined,
            ),
          ],
        ),
      const SizedBox(height: 24),
      const Text(
        'Your workspace',
        style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
      ),
      const SizedBox(height: 12),
      Wrap(
        spacing: 12,
        runSpacing: 12,
        children: [
          for (final m in [
            'Point of sale',
            'Orders',
            'Kitchen',
            'Shifts',
            'Tables',
            'Catalogue',
            if (api.can('ManageInventory')) 'Inventory',
            if (api.can('ViewReports')) 'Reports',
          ])
            SizedBox(
              width: 155,
              child: Card(
                child: ListTile(
                  title: Text(
                    m,
                    style: const TextStyle(fontWeight: FontWeight.w700),
                  ),
                  trailing: const Icon(Icons.arrow_forward, size: 18),
                  onTap: () => widget.onNavigate(m),
                ),
              ),
            ),
        ],
      ),
    ],
  );
  Widget metric(String label, String value, IconData icon) => SizedBox(
    width: 175,
    child: Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, color: navy),
            const SizedBox(height: 18),
            Text(
              value,
              style: const TextStyle(fontSize: 23, fontWeight: FontWeight.w800),
            ),
            Text(
              label,
              style: const TextStyle(color: Colors.blueGrey, fontSize: 12),
            ),
          ],
        ),
      ),
    ),
  );
  Widget record(dynamic row) {
    final r = Map<String, dynamic>.from(row);
    return switch (module) {
      'Orders' => ListTile(
        leading: const Icon(Icons.receipt_long_outlined),
        title: Text('${r['orderNumber']}'),
        subtitle: Text(
          '${r['status']} · ${r['serviceType']} · ${dateLabel(r['createdAt'])}',
        ),
        trailing: Text(
          formatInr(money(r['totalAmount'])),
          style: const TextStyle(fontWeight: FontWeight.w800),
        ),
        onTap: busy ? null : () => showOrder(r),
      ),
      'Tables' => ListTile(
        leading: Icon(
          Icons.table_restaurant,
          color: r['hasOpenOrder'] == true ? Colors.deepOrange : Colors.teal,
        ),
        title: Text('Table ${r['tableNumber']}'),
        subtitle: Text(
          '${r['capacity']} seats · ${r['zoneName'] ?? 'Main floor'}',
        ),
        trailing: Text(r['hasOpenOrder'] == true ? 'Occupied' : 'Free'),
      ),
      'Catalogue' => ListTile(
        leading: api.imageUrl(r['imageUrl']) != null
            ? ClipRRect(
                borderRadius: BorderRadius.circular(10),
                child: Image.network(
                  api.imageUrl(r['imageUrl'])!,
                  width: 52,
                  height: 52,
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) =>
                      const Icon(Icons.image_not_supported_outlined),
                ),
              )
            : CircleAvatar(
                backgroundColor: gold.withValues(alpha: .15),
                child: Text('${r['name']}'.substring(0, 1)),
              ),
        title: Text('${r['name']}'),
        subtitle: Text(
          '${r['sku']} · ${r['categoryName']} · ${r['isAvailable'] == true ? 'Available' : 'Unavailable'}',
        ),
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              formatInr(money(r['price'])),
              style: const TextStyle(fontWeight: FontWeight.w800),
            ),
            if (api.canManageCatalogue)
              PopupMenuButton<String>(
                onSelected: (value) {
                  if (value == 'edit') {
                    addProduct(r);
                  } else if (value == 'upload') {
                    productImage(r);
                  } else {
                    run(() async {
                      await api.request(
                        '/products/${r['id']}/image',
                        method: 'DELETE',
                      );
                    });
                  }
                },
                itemBuilder: (_) => [
                  const PopupMenuItem(value: 'edit', child: Text('Edit item')),
                  const PopupMenuItem(
                    value: 'upload',
                    child: Text('Upload image'),
                  ),
                  if (r['imageUrl'] != null)
                    const PopupMenuItem(
                      value: 'remove',
                      child: Text('Remove image'),
                    ),
                ],
              ),
          ],
        ),
      ),
      'Super Admin' || 'My Outlet' => ListTile(
        leading: const Icon(Icons.storefront_outlined),
        title: Text('${r['name']}'),
        subtitle: Text(
          '${r['address'] ?? ''}\n${r['isOperating'] == true ? 'Operating' : 'Closed'} · INR',
        ),
        trailing: PopupMenuButton<String>(
          onSelected: (value) async {
            if (value == 'edit') {
              outletForm(r);
            } else if (value == 'qr') {
              outletQr(r);
            } else {
              await api.selectOutlet(r['id'], r['name']);
              widget.onNavigate('Overview');
            }
          },
          itemBuilder: (_) => [
            const PopupMenuItem(value: 'edit', child: Text('Configure outlet')),
            const PopupMenuItem(value: 'qr', child: Text('Public ordering QR')),
            if (api.user?.roleName == 'Owner')
              const PopupMenuItem(
                value: 'use',
                child: Text('Use this outlet workspace'),
              ),
          ],
        ),
      ),
      'Inventory' => ListTile(
        leading: Icon(
          Icons.inventory_2_outlined,
          color: r['isLowStock'] == true ? Colors.red : Colors.teal,
        ),
        title: Text('${r['productName']}'),
        subtitle: Text(
          '${r['sku']} · ${r['isLowStock'] == true ? 'LOW STOCK' : 'Stock available'}',
        ),
        trailing: Text(
          '${r['currentStock']}',
          style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 20),
        ),
      ),
      'Staff' => ListTile(
        leading: const Icon(Icons.person_outline),
        title: Text('${r['username']}'),
        subtitle: Text(
          '${r['roleName']} · ${r['email']}\n${r['isActive'] == true ? 'Active' : 'Inactive'}',
        ),
        trailing: const Icon(Icons.edit_outlined),
        onTap: busy ? null : () => staffForm(r),
      ),
      'Audit log' => ListTile(
        leading: const Icon(Icons.policy_outlined),
        title: Text('${r['action']} · ${r['entity']} #${r['entityId']}'),
        subtitle: Text(
          '${r['actorName']} · ${dateLabel(r['createdAt'])}\n${r['reason'] ?? ''}',
        ),
      ),
      'Shifts' => ListTile(
        leading: const Icon(Icons.schedule),
        title: Text('Shift #${r['id']} · ${r['userName']}'),
        subtitle: Text(
          '${r['status']} · ${dateLabel(r['openTime'])}\nFloat ${formatInr(money(r['openingFloat']))} · Variance ${formatInr(money(r['variance']))}',
        ),
        trailing: api.can('ViewReports')
            ? TextButton(
                onPressed: busy ? null : () => zReport(r['id']),
                child: const Text('Z-report'),
              )
            : null,
      ),
      'Reports' => Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${r['groupKey']} · ${r['orderCount']} orders',
              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 20,
              runSpacing: 8,
              children: [
                for (final key in [
                  'grossSales',
                  'discounts',
                  'taxes',
                  'netSales',
                  'cash',
                  'card',
                  'digitalWallet',
                ])
                  Text('$key: ${formatInr(money(r[key]))}'),
              ],
            ),
          ],
        ),
      ),
      'Kitchen' => kitchenCard(r),
      _ => const SizedBox.shrink(),
    };
  }

  Widget kitchenCard(Json ticket) {
    final age = DateTime.now()
        .difference(DateTime.parse(ticket['createdAt']).toLocal())
        .inMinutes;
    const next = {
      'New': 'Accepted',
      'Accepted': 'InProgress',
      'InProgress': 'Ready',
      'Ready': 'Served',
    };
    final status = '${ticket['status']}';
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '${ticket['orderNumber']}',
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              Chip(
                label: Text('$age min'),
                backgroundColor: age >= 15 ? const Color(0xffffe2de) : canvas,
              ),
            ],
          ),
          Text(
            '${ticket['stationName']} · Table ${ticket['tableNumber'] ?? 'Takeaway'} · $status',
          ),
          const Divider(height: 24),
          ...((ticket['items'] as List?) ?? []).map(
            (i) => Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '${i['quantity']} × ${i['productName']}${i['notes'] == null || i['notes'] == '' ? '' : '\n  ${i['notes']}'}',
              ),
            ),
          ),
          if (next.containsKey(status))
            FilledButton(
              onPressed: busy
                  ? null
                  : () => run(
                      () => api.updateTicketStatus(ticket['id'], next[status]!),
                    ),
              child: Text('Mark ${next[status]}'),
            ),
        ],
      ),
    );
  }

  List<Widget> get tools => switch (module) {
    'Tables' =>
      api.can('ManageSettings') || api.user?.roleName == 'StoreManager'
          ? [
              FilledButton.icon(
                onPressed: busy ? null : addTable,
                icon: const Icon(Icons.add),
                label: const Text('Add table'),
              ),
            ]
          : [],
    'Catalogue' =>
      api.canManageCatalogue
          ? [
              FilledButton.icon(
                onPressed: busy ? null : addProduct,
                icon: const Icon(Icons.add),
                label: const Text('Product'),
              ),
              OutlinedButton(
                onPressed: busy ? null : addCategory,
                child: const Text('Category'),
              ),
            ]
          : [],
    'Inventory' => [
      FilledButton.icon(
        onPressed: busy ? null : inventoryMovement,
        icon: const Icon(Icons.add),
        label: const Text('Movement'),
      ),
      OutlinedButton(
        onPressed: busy ? null : inventoryHistory,
        child: const Text('Ledger'),
      ),
      OutlinedButton(
        onPressed: busy ? null : addRecipe,
        child: const Text('Recipe'),
      ),
    ],
    'Staff' => [
      FilledButton.icon(
        onPressed: busy ? null : () => staffForm(),
        icon: const Icon(Icons.person_add_outlined),
        label: const Text('Add staff'),
      ),
    ],
    'Reports' => [
      OutlinedButton.icon(
        onPressed: busy ? null : filterReport,
        icon: const Icon(Icons.filter_list),
        label: const Text('Filters'),
      ),
      if (api.can('ExportReports'))
        FilledButton.icon(
          onPressed: busy
              ? null
              : () => run(
                  () => printSales(
                    api,
                    rows,
                    outletId: loadedOutletId ?? api.outletId,
                  ),
                ),
          icon: const Icon(Icons.print_outlined),
          label: const Text('Print / PDF'),
        ),
    ],
    'Super Admin' => [
      FilledButton.icon(
        onPressed: busy ? null : () => outletForm(),
        icon: const Icon(Icons.add_business_outlined),
        label: const Text('Add outlet'),
      ),
    ],
    _ => [],
  };
  @override
  Widget build(BuildContext context) {
    if (loading) return const Center(child: CircularProgressIndicator());
    if (error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.cloud_off_outlined, size: 48),
              const SizedBox(height: 16),
              Text(error!, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(onPressed: load, child: const Text('Retry')),
            ],
          ),
        ),
      );
    }
    return Column(
      children: [
        if (busy) const LinearProgressIndicator(minHeight: 3),
        Expanded(
          child: RefreshIndicator(
            onRefresh: load,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.all(16),
              children: [
                if (module == 'Overview')
                  overview()
                else ...[
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          module,
                          style: const TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                      ),
                      IconButton(
                        tooltip: 'Refresh',
                        onPressed: busy ? null : load,
                        icon: const Icon(Icons.refresh),
                      ),
                    ],
                  ),
                  if (tools.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      child: Wrap(spacing: 8, runSpacing: 8, children: tools),
                    ),
                  if (module == 'Orders')
                    Padding(
                      padding: const EdgeInsets.only(bottom: 16),
                      child: TextField(
                        decoration: const InputDecoration(
                          hintText: 'Search order or table · press enter',
                          prefixIcon: Icon(Icons.search),
                        ),
                        onSubmitted: (v) {
                          filter = v;
                          page = 1;
                          load();
                        },
                      ),
                    ),
                  if (module == 'Shifts')
                    Padding(
                      padding: const EdgeInsets.only(bottom: 20),
                      child: Card(
                        color: navy,
                        child: Padding(
                          padding: const EdgeInsets.all(22),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Text(
                                shift == null
                                    ? 'Start your day'
                                    : 'Shift #${shift['id']} active',
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 24,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                              const SizedBox(height: 8),
                              Text(
                                shift == null
                                    ? 'Open a shift before taking orders.'
                                    : 'Float ${formatInr(money(shift['openingFloat']))} · ${dateLabel(shift['openTime'])}',
                                style: const TextStyle(color: Colors.white70),
                              ),
                              const SizedBox(height: 18),
                              if (shift == null || api.can('CloseShift'))
                                FilledButton.tonal(
                                  onPressed: busy
                                      ? null
                                      : shift == null
                                      ? openShift
                                      : closeShift,
                                  child: Text(
                                    shift == null
                                        ? 'Open shift'
                                        : 'Count cash & close',
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  if (rows.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(40),
                      child: Center(
                        child: Text('No records found. Pull down to refresh.'),
                      ),
                    ),
                  ...rows
                      .where(
                        (r) =>
                            module != 'Kitchen' ||
                            !['Served', 'Cancelled'].contains(r['status']),
                      )
                      .map(
                        (r) => Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: Card(child: record(r)),
                        ),
                      ),
                  if (['Orders', 'Audit log', 'Shifts'].contains(module))
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        TextButton(
                          onPressed: page > 1 && !busy
                              ? () {
                                  page--;
                                  load();
                                }
                              : null,
                          child: const Text('Previous'),
                        ),
                        Text('Page $page'),
                        TextButton(
                          onPressed: rows.length == 20 && !busy
                              ? () {
                                  page++;
                                  load();
                                }
                              : null,
                          child: const Text('Next'),
                        ),
                      ],
                    ),
                ],
              ],
            ),
          ),
        ),
      ],
    );
  }
}
