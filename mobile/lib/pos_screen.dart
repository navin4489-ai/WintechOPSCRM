import 'package:flutter/material.dart';

import 'api_service.dart';
import 'brand.dart';
import 'currency.dart';
import 'ui_helpers.dart';

class PosScreen extends StatefulWidget {
  final ApiService api;
  final Json? initialOrder;
  const PosScreen({super.key, required this.api, this.initialOrder});
  @override
  State<PosScreen> createState() => _PosScreenState();
}

class _PosScreenState extends State<PosScreen> {
  List<dynamic> tables = [], products = [];
  Json? order;
  bool loading = true, busy = false;
  String? error;
  String query = '', category = 'All';
  int tab = 0;
  ApiService get api => widget.api;
  @override
  void initState() {
    super.initState();
    order = widget.initialOrder;
    tab = order == null ? 0 : 2;
    load();
  }

  Future<void> load() async {
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final result = await Future.wait([
        api.request('/tables/outlet/${api.outletId}'),
        api.request('/products?outletId=${api.outletId}&availableOnly=true'),
      ]);
      if (mounted) {
        setState(() {
          tables = result[0];
          products = result[1];
        });
      }
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> run(Future<void> Function() task) async {
    if (busy) return;
    setState(() => busy = true);
    try {
      await task();
    } catch (e) {
      if (mounted) notify(context, e);
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> selectTable(dynamic table) => run(() async {
    final data = table['hasOpenOrder'] == true
        ? await api.request('/orders/table/${table['id']}/open')
        : await api.request(
            '/orders',
            method: 'POST',
            body: {
              'outletId': api.outletId,
              'tableId': table['id'],
              'serviceType': 'DineIn',
              'items': [],
            },
          );
    if (data == null) {
      throw const ApiException('No open order. Refresh the table list.');
    }
    if (mounted) {
      setState(() {
        order = Map<String, dynamic>.from(data);
        tab = 1;
      });
    }
  });
  Future<void> newSale() async {
    final values = await askForm(context, 'New sale', [
      const FormFieldSpec(
        'serviceType',
        'Service',
        initial: 'Takeaway',
        choices: {
          'Takeaway': 'Takeaway',
          'QuickService': 'Quick service',
          'Retail': 'Retail',
        },
      ),
    ]);
    if (values == null) return;
    await run(() async {
      final result = await api.request(
        '/orders',
        method: 'POST',
        body: {
          'outletId': api.outletId,
          'serviceType': values['serviceType'],
          'items': [],
        },
      );
      if (mounted) {
        setState(() {
          order = Map<String, dynamic>.from(result);
          tab = 1;
        });
      }
    });
  }

  Future<void> addProduct(dynamic product) async {
    if (order == null || order!['status'] != 'Open') {
      notify(context, 'Select a table or start a new sale first.');
      return;
    }
    final groups = (product['modifierGroups'] as List?) ?? [];
    final fields = [
      const FormFieldSpec('quantity', 'Quantity', number: true, initial: '1'),
      const FormFieldSpec('notes', 'Kitchen notes', optional: true),
      ...groups.map(
        (g) => FormFieldSpec(
          'modifier_${g['id']}',
          '${g['name']}',
          optional: g['isRequired'] != true,
          multi: true,
          minSelections:
              (g['minSelections'] as int? ?? (g['isRequired'] == true ? 1 : 0)),
          maxSelections: (g['maxSelections'] as int? ?? 1),
          choices: {
            for (final option in (g['options'] as List? ?? []))
              '${option['id']}':
                  '${option['name']} (+${formatInr(money(option['priceImpact']))})',
          },
        ),
      ),
    ];
    final values = await askForm(
      context,
      '${product['name']}',
      fields,
      submit: 'Add to order',
    );
    if (values == null) return;
    final qty = values['quantity'] as num;
    if (qty <= 0 || qty != qty.round()) {
      if (mounted) notify(context, 'Quantity must be a positive whole number.');
      return;
    }
    await run(() async {
      final result = await api.request(
        '/orders/${order!['id']}/items',
        method: 'POST',
        body: {
          'productId': product['id'],
          'quantity': qty.toInt(),
          'notes': values['notes'],
          'modifierOptionIds': values.entries
              .where(
                (e) => e.key.startsWith('modifier_') && '${e.value}'.isNotEmpty,
              )
              .expand(
                (e) => '${e.value}'
                    .split(',')
                    .where((v) => v.isNotEmpty)
                    .map(int.parse),
              )
              .toList(),
        },
      );
      if (mounted) setState(() => order = Map<String, dynamic>.from(result));
    });
  }

  double get due =>
      ((money(order?['totalAmount']) -
                  ((order?['payments'] as List?) ?? [])
                      .where((p) => p['status'] == 'Captured')
                      .fold<double>(0, (s, p) => s + money(p['amount']))) *
              100)
          .round() /
      100;
  Future<void> pay() async {
    final values = await askForm(
      context,
      'Take payment · ${formatInr(due)}',
      [
        const FormFieldSpec(
          'tenderType',
          'Tender',
          initial: 'Cash',
          choices: {
            'Cash': 'Cash',
            'Card': 'Card (externally collected)',
            'UPI': 'UPI (externally confirmed)',
          },
        ),
        FormFieldSpec(
          'received',
          'Amount received',
          number: true,
          initial: due.toStringAsFixed(2),
        ),
      ],
      warning: 'Card/UPI records an already confirmed external payment. This app does not charge a bank or card.',
      submit: 'Record payment',
    );
    if (values == null) return;
    final received = money(values['received']);
    if (received < due) {
      if (mounted) {
        notify(context, 'Amount received is below the outstanding balance.');
      }
      return;
    }
    if (values['tenderType'] != 'Cash' && received != due) {
      if (mounted) {
        notify(context, 'Card/UPI must match the outstanding balance.');
      }
      return;
    }
    final change = received - due;
    await run(() async {
      final result = await api.request(
        '/orders/${order!['id']}/payment',
        method: 'POST',
        body: {
          'orderId': order!['id'],
          'tenderType': values['tenderType'],
          'amount': due,
        },
      );
      if (mounted) {
        setState(() {
          order = Map<String, dynamic>.from(result);
          tab = 2;
        });
        notify(context, 'Payment recorded. Change due ${formatInr(change)}');
      }
      await load();
    });
  }

  Future<void> discount() async {
    final values = await askForm(context, 'Order discount', [
      const FormFieldSpec(
        'mode',
        'Type',
        initial: 'percent',
        choices: {'percent': 'Percentage', 'amount': 'Flat INR amount'},
      ),
      const FormFieldSpec('value', 'Discount', number: true),
      const FormFieldSpec('reason', 'Reason'),
      ...approvalFields(api),
    ]);
    if (values == null) return;
    await run(() async {
      final data = await api.request(
        '/orders/${order!['id']}/discount',
        method: 'POST',
        body: {
          values['mode']: values['value'],
          'reason': values['reason'],
          ...approvalValues(values),
        },
      );
      if (mounted) setState(() => order = Map<String, dynamic>.from(data));
    });
  }

  Future<void> voidSale() async {
    final value = await askForm(
      context,
      'Void this order?',
      [const FormFieldSpec('reason', 'Reason'), ...approvalFields(api)],
      warning: 'This cancels the order. Paid orders must be refunded instead.',
    );
    if (value == null) return;
    await run(() async {
      await api.request(
        '/orders/${order!['id']}/void',
        method: 'POST',
        body: {
          'orderId': order!['id'],
          'reason': value['reason'],
          ...approvalValues(value),
        },
      );
      if (mounted) setState(() => order = null);
      await load();
    });
  }

  Future<void> transfer() async {
    final free = tables.where((t) => t['hasOpenOrder'] != true).toList();
    if (free.isEmpty) {
      notify(context, 'No free tables available.');
      return;
    }
    final value = await askForm(context, 'Transfer table', [
      FormFieldSpec(
        'tableId',
        'Destination',
        choices: {
          for (final t in free) '${t['id']}': 'Table ${t['tableNumber']}',
        },
      ),
    ]);
    if (value == null) return;
    await run(() async {
      final data = await api.request(
        '/orders/${order!['id']}/transfer-table?newTableId=${value['tableId']}',
        method: 'POST',
      );
      if (mounted) setState(() => order = Map<String, dynamic>.from(data));
      await load();
    });
  }

  Future<void> merge() async {
    final otherTables = tables
        .where((t) => t['hasOpenOrder'] == true && t['id'] != order?['tableId'])
        .toList();
    final value = await askForm(
      context,
      'Merge into another bill',
      [
        FormFieldSpec(
          'tableId',
          'Target table',
          choices: {
            for (final t in otherTables)
              '${t['id']}': 'Table ${t['tableNumber']}',
          },
        ),
      ],
      warning: 'The current bill moves into the target bill. Both bills must have no discounts or payments.',
    );
    if (value == null) return;
    await run(() async {
      final target = await api.request(
        '/orders/table/${value['tableId']}/open',
      );
      if (target == null) {
        throw const ApiException('Target order not found. Refresh tables.');
      }
      final result = await api.request(
        '/orders/${order!['id']}/merge/${target['id']}',
        method: 'POST',
      );
      if (mounted) setState(() => order = Map<String, dynamic>.from(result));
      await load();
    });
  }

  Future<void> split() async {
    final items = (order?['orderItems'] as List?) ?? [];
    final free = tables.where((t) => t['hasOpenOrder'] != true).toList();
    if (items.length < 2 || free.isEmpty) {
      notify(context, 'Split needs at least two line items and a free table.');
      return;
    }
    final value = await askForm(
      context,
      'Split items to a new bill',
      [
        FormFieldSpec(
          'tableId',
          'New table',
          choices: {
            for (final t in free) '${t['id']}': 'Table ${t['tableNumber']}',
          },
        ),
        FormFieldSpec(
          'itemId',
          'Item to move',
          choices: {
            for (final i in items)
              '${i['id']}': '${i['quantity']} × ${i['productName']}',
          },
        ),
      ],
      warning: 'Move one whole line to a new bill. Remove discounts first; partially paid bills cannot be split.',
    );
    if (value == null) return;
    await run(() async {
      final result = await api.request(
        '/orders/${order!['id']}/split?newTableId=${value['tableId']}',
        method: 'POST',
        body: [int.parse(value['itemId'])],
      );
      if (mounted) setState(() => order = Map<String, dynamic>.from(result));
      await load();
    });
  }

  Widget tablesView() => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      Row(
        children: [
          const Expanded(
            child: Text(
              'Floor & tables',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 20),
            ),
          ),
          FilledButton.icon(
            onPressed: busy ? null : newSale,
            icon: const Icon(Icons.add),
            label: const Text('New sale'),
          ),
        ],
      ),
      const SizedBox(height: 12),
      const Text(
        'Open a shift before starting an order. Existing orders can be resumed here.',
      ),
      const SizedBox(height: 16),
      Wrap(
        spacing: 12,
        runSpacing: 12,
        children: tables
            .map(
              (t) => SizedBox(
                width: 155,
                height: 185,
                child: Card(
                  color: t['hasOpenOrder'] == true
                      ? const Color(0xfffff3d9)
                      : Colors.white,
                  child: InkWell(
                    borderRadius: BorderRadius.circular(20),
                    onTap: busy ? null : () => selectTable(t),
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(
                            Icons.table_restaurant_outlined,
                            color: t['hasOpenOrder'] == true
                                ? Colors.deepOrange
                                : Colors.teal,
                          ),
                          const Spacer(),
                          Text(
                            'Table ${t['tableNumber']}',
                            style: const TextStyle(
                              fontSize: 19,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          Text(
                            '${t['hasOpenOrder'] == true ? 'Occupied' : 'Available'} · ${t['capacity']} seats',
                            style: const TextStyle(fontSize: 12),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            )
            .toList(),
      ),
    ],
  );
  Widget menuView() {
    final categories = [
      'All',
      ...products.map((p) => '${p['categoryName'] ?? 'Other'}').toSet(),
    ];
    final filtered = products
        .where(
          (p) =>
              (category == 'All' || category == p['categoryName']) &&
              '${p['name']} ${p['sku']} ${p['barcode'] ?? ''}'
                  .toLowerCase()
                  .contains(query.toLowerCase()),
        )
        .toList();
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(16),
          child: TextField(
            decoration: const InputDecoration(
              hintText: 'Search name, SKU or scan barcode',
              prefixIcon: Icon(Icons.search),
            ),
            onChanged: (v) => setState(() => query = v),
            onSubmitted: (v) {
              final matches = products
                  .where((p) => p['barcode'] == v || p['sku'] == v)
                  .toList();
              if (matches.length == 1) addProduct(matches.first);
            },
          ),
        ),
        SizedBox(
          height: 48,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            children: categories
                .map(
                  (c) => Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(c),
                      selected: category == c,
                      onSelected: (_) => setState(() => category = c),
                    ),
                  ),
                )
                .toList(),
          ),
        ),
        Expanded(
          child: GridView.builder(
            padding: const EdgeInsets.all(16),
            gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
              maxCrossAxisExtent: 220,
              mainAxisExtent: 220,
              crossAxisSpacing: 12,
              mainAxisSpacing: 12,
            ),
            itemCount: filtered.length,
            itemBuilder: (_, i) {
              final p = filtered[i];
              return Card(
                child: InkWell(
                  onTap: busy ? null : () => addProduct(p),
                  borderRadius: BorderRadius.circular(20),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (api.imageUrl(p['imageUrl']) != null)
                          ClipRRect(
                            borderRadius: BorderRadius.circular(12),
                            child: Image.network(
                              api.imageUrl(p['imageUrl'])!,
                              height: 76,
                              width: double.infinity,
                              fit: BoxFit.cover,
                              errorBuilder: (_, _, _) => const SizedBox(
                                height: 76,
                                child: Icon(Icons.image_not_supported_outlined),
                              ),
                            ),
                          )
                        else
                          CircleAvatar(
                            backgroundColor: gold.withValues(alpha: .18),
                            child: Text(
                              '${p['name']}'.substring(0, 1),
                              style: const TextStyle(color: navy),
                            ),
                          ),
                        const SizedBox(height: 12),
                        Text(
                          '${p['name']}',
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontWeight: FontWeight.w700),
                        ),
                        const Spacer(),
                        Text(
                          formatInr(money(p['price'])),
                          style: const TextStyle(
                            color: navy,
                            fontWeight: FontWeight.w800,
                            fontSize: 18,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget orderView() {
    if (order == null) {
      return const Center(
        child: Text('Select a table or start a takeaway / retail sale.'),
      );
    }
    final items = (order!['orderItems'] as List?) ?? [];
    final open = order!['status'] == 'Open';
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          '${order!['orderNumber']}',
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
        ),
        Text(
          '${order!['serviceType']} · ${order!['status']} · Table ${order!['tableNumber'] ?? '—'}',
        ),
        if (order!['notes'] != null && '${order!['notes']}'.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: SelectableText('${order!['notes']}'),
          ),
        const SizedBox(height: 16),
        ...items.map(
          (i) => Card(
            child: ListTile(
              title: Text('${i['quantity']} × ${i['productName']}'),
              subtitle: Text(
                '${formatInr(money(i['lineTotal']))}${i['notes'] == null || i['notes'] == '' ? '' : '\n${i['notes']}'}',
              ),
              trailing: open
                  ? IconButton(
                      tooltip: 'Remove item',
                      onPressed: busy
                          ? null
                          : () => run(() async {
                              final data = await api.request(
                                '/orders/${order!['id']}/items/${i['id']}',
                                method: 'DELETE',
                              );
                              if (mounted) {
                                setState(
                                  () => order = Map<String, dynamic>.from(data),
                                );
                              }
                            }),
                      icon: const Icon(
                        Icons.remove_circle_outline,
                        color: Colors.red,
                      ),
                    )
                  : null,
            ),
          ),
        ),
        const SizedBox(height: 20),
        ...{
          'Subtotal': order!['subtotal'],
          'Discount': order!['discountAmount'],
          'GST': order!['taxAmount'],
          'Grand total': order!['totalAmount'],
          'Outstanding': due,
        }.entries.map(
          (e) => Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(e.key),
                Text(
                  formatInr(money(e.value)),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 18,
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 18),
        if (open && items.isNotEmpty)
          FilledButton.icon(
            onPressed: busy ? null : pay,
            icon: const Icon(Icons.payments_outlined),
            label: Text('Take payment · ${formatInr(due)}'),
          ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (open && api.can('ApplyDiscount'))
              OutlinedButton(
                onPressed: busy || items.isEmpty ? null : discount,
                child: const Text('Discount'),
              ),
            if (open && api.can('TransferTable'))
              OutlinedButton(
                onPressed: busy ? null : transfer,
                child: const Text('Transfer'),
              ),
            if (open && api.can('MergeTable'))
              OutlinedButton(
                onPressed: busy ? null : merge,
                child: const Text('Merge'),
              ),
            if (open && api.can('SplitTable'))
              OutlinedButton(
                onPressed: busy ? null : split,
                child: const Text('Split'),
              ),
            if (open && api.can('VoidOrder'))
              OutlinedButton(
                onPressed: busy ? null : voidSale,
                child: const Text('Void'),
              ),
            OutlinedButton.icon(
              onPressed: busy
                  ? null
                  : () => run(() => printReceipt(api, order!)),
              icon: const Icon(Icons.print_outlined),
              label: const Text('Receipt / print'),
            ),
          ],
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    if (loading) return const Center(child: CircularProgressIndicator());
    if (error != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(error!, textAlign: TextAlign.center),
            FilledButton(onPressed: load, child: const Text('Retry')),
          ],
        ),
      );
    }
    return Column(
      children: [
        if (busy) const LinearProgressIndicator(minHeight: 3),
        Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Expanded(
                child: SegmentedButton<int>(
                  segments: const [
                    ButtonSegment(
                      value: 0,
                      icon: Icon(Icons.table_restaurant),
                      label: Text('Tables'),
                    ),
                    ButtonSegment(
                      value: 1,
                      icon: Icon(Icons.menu_book),
                      label: Text('Menu'),
                    ),
                    ButtonSegment(
                      value: 2,
                      icon: Icon(Icons.receipt_long),
                      label: Text('Bill'),
                    ),
                  ],
                  selected: {tab},
                  onSelectionChanged: (v) => setState(() => tab = v.first),
                ),
              ),
              IconButton(
                tooltip: 'Refresh',
                onPressed: busy ? null : load,
                icon: const Icon(Icons.refresh),
              ),
            ],
          ),
        ),
        if (order != null)
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Text(
              '${order!['orderNumber']} · ${formatInr(money(order!['totalAmount']))}',
              style: const TextStyle(color: navy, fontWeight: FontWeight.w600),
            ),
          ),
        Expanded(
          child: LayoutBuilder(
            builder: (_, c) => c.maxWidth >= 1000
                ? Row(
                    children: [
                      Expanded(child: tab == 0 ? tablesView() : menuView()),
                      const VerticalDivider(width: 1),
                      SizedBox(width: 390, child: orderView()),
                    ],
                  )
                : switch (tab) {
                    0 => tablesView(),
                    1 => menuView(),
                    _ => orderView(),
                  },
          ),
        ),
      ],
    );
  }
}
