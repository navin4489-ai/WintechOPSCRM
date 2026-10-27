import 'package:flutter/material.dart';

import 'dart:convert';

import 'package:pdf/widgets.dart' as pw;
import 'package:printing/printing.dart';

import 'api_service.dart';
import 'currency.dart';

typedef Json = Map<String, dynamic>;
double money(dynamic value) => (value as num?)?.toDouble() ?? 0;
String dateLabel(dynamic value) {
  final d = DateTime.tryParse('$value')?.toLocal();
  return d == null
      ? '—'
      : '${d.day}/${d.month}/${d.year} ${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
}

void notify(BuildContext context, Object message) {
  ScaffoldMessenger.of(context)
      .showSnackBar(SnackBar(content: Text(message.toString())));
}

class FormFieldSpec {
  final String key, label;
  final bool number, optional, secret;
  final bool multi;
  final int minSelections, maxSelections;
  final String initial;
  final Map<String, String>? choices;
  const FormFieldSpec(
    this.key,
    this.label, {
    this.number = false,
    this.optional = false,
    this.secret = false,
    this.initial = '',
    this.choices,
    this.multi = false,
    this.minSelections = 0,
    this.maxSelections = 99,
  });
}

List<FormFieldSpec> approvalFields(ApiService api) =>
    api.can('ApproveManagerAction')
    ? []
    : [
        const FormFieldSpec('approverUsername', 'Approving manager username'),
        const FormFieldSpec(
          'approverPassword',
          'Manager password (not saved)',
          secret: true,
        ),
      ];
Json approvalValues(Json values) => {
  'approverUsername': values['approverUsername'],
  'approverPassword': values['approverPassword'],
};

Future<Json?> askForm(
  BuildContext context,
  String title,
  List<FormFieldSpec> fields, {
  String submit = 'Continue',
  String? warning,
}) => showDialog<Json>(
  context: context,
  builder: (_) => _ActionForm(
    title: title,
    fields: fields,
    submit: submit,
    warning: warning,
  ),
);

class _ActionForm extends StatefulWidget {
  final String title, submit;
  final String? warning;
  final List<FormFieldSpec> fields;
  const _ActionForm({
    required this.title,
    required this.fields,
    required this.submit,
    this.warning,
  });
  @override
  State<_ActionForm> createState() => _ActionFormState();
}

class _ActionFormState extends State<_ActionForm> {
  final key = GlobalKey<FormState>();
  late final controllers = {
    for (final f in widget.fields)
      f.key: TextEditingController(text: f.initial),
  };
  @override
  void dispose() {
    for (final c in controllers.values) {
      c.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.title),
    content: SizedBox(
      width: 440,
      child: SingleChildScrollView(
        child: Form(
          key: key,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.warning != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: Text(
                    widget.warning!,
                    style: const TextStyle(color: Colors.deepOrange),
                  ),
                ),
              ...widget.fields.map(
                (f) => Padding(
                  padding: const EdgeInsets.only(bottom: 16),
                  child: f.multi && f.choices != null
                      ? FormField<Set<String>>(
                          initialValue: <String>{},
                          validator: (v) =>
                              (v?.length ?? 0) < f.minSelections ||
                                  (v?.length ?? 0) > f.maxSelections
                              ? 'Select ${f.minSelections}–${f.maxSelections} options'
                              : null,
                          builder: (state) => Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                f.label,
                                style: const TextStyle(
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              ...f.choices!.entries.map(
                                (e) => CheckboxListTile(
                                  contentPadding: EdgeInsets.zero,
                                  title: Text(e.value),
                                  value: state.value!.contains(e.key),
                                  onChanged: (selected) {
                                    final next = {...state.value!};
                                    if (selected == true) {
                                      if (f.maxSelections == 1) next.clear();
                                      next.add(e.key);
                                    } else {
                                      next.remove(e.key);
                                    }
                                    controllers[f.key]!.text = next.join(',');
                                    state.didChange(next);
                                  },
                                ),
                              ),
                              if (state.hasError)
                                Text(
                                  state.errorText!,
                                  style: const TextStyle(color: Colors.red),
                                ),
                            ],
                          ),
                        )
                      : f.choices != null
                      ? DropdownButtonFormField<String>(
                          isExpanded: true,
                          initialValue: f.choices!.containsKey(f.initial)
                              ? f.initial
                              : null,
                          decoration: InputDecoration(labelText: f.label),
                          items: f.choices!.entries
                              .map(
                                (e) => DropdownMenuItem(
                                  value: e.key,
                                  child: Text(
                                    e.value,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              )
                              .toList(),
                          onChanged: (v) => controllers[f.key]!.text = v ?? '',
                          validator: (v) =>
                              !f.optional && (v == null || v.isEmpty)
                              ? 'Choose ${f.label.toLowerCase()}'
                              : null,
                        )
                      : TextFormField(
                          controller: controllers[f.key],
                          obscureText: f.secret,
                          keyboardType: f.number
                              ? const TextInputType.numberWithOptions(
                                  decimal: true,
                                  signed: true,
                                )
                              : TextInputType.text,
                          decoration: InputDecoration(labelText: f.label),
                          validator: (v) {
                            if (!f.optional &&
                                (v == null || v.trim().isEmpty)) {
                              return '${f.label} is required';
                            }
                            if (f.number &&
                                v != null &&
                                v.isNotEmpty &&
                                (double.tryParse(v) == null ||
                                    !double.parse(v).isFinite)) {
                              return 'Enter a valid number';
                            }
                            return null;
                          },
                        ),
                ),
              ),
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: () {
          if (!key.currentState!.validate()) return;
          Navigator.pop(context, {
            for (final f in widget.fields)
              f.key: f.number
                  ? num.tryParse(controllers[f.key]!.text)
                  : f.secret
                  ? controllers[f.key]!.text
                  : controllers[f.key]!.text.trim(),
          });
        },
        child: Text(widget.submit),
      ),
    ],
  );
}

Future<void> printReceipt(ApiService api, Json order) async {
  // The backend records every receipt/reprint before the Android print dialog.
  final data = await api.request(
    '/orders/${order['id']}/receipt',
    method: 'POST',
  );
  final receipt = Map<String, dynamic>.from(data);
  final document = pw.Document();
  String inr(dynamic value) => formatInr(money(value)).replaceAll('₹', 'INR ');
  document.addPage(
    pw.MultiPage(
      build: (_) => [
        pw.Text(
          'ORO POS',
          style: pw.TextStyle(fontSize: 24, fontWeight: pw.FontWeight.bold),
        ),
        pw.Text(api.user?.outletName ?? 'Outlet'),
        pw.SizedBox(height: 12),
        pw.Text('TRANSACTION RECEIPT - ${receipt['orderNumber']}'),
        pw.Text(
          '${receipt['serviceType']} | Table ${receipt['tableNumber'] ?? '-'} | ${receipt['status']}',
        ),
        pw.Text(dateLabel(receipt['createdAt'])),
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
                pw.Text(inr(i['lineTotal'])),
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
            children: [pw.Text(e.key), pw.Text(inr(e.value))],
          ),
        ),
        pw.SizedBox(height: 16),
        pw.Text('Thank you. Keep this receipt for your records.'),
      ],
    ),
  );
  await Printing.layoutPdf(
    name: 'ORO-${receipt['orderNumber']}',
    onLayout: (_) async => document.save(),
  );
}

Future<void> printSales(List<dynamic> rows) async {
  final doc = pw.Document();
  doc.addPage(
    pw.MultiPage(
      build: (_) => [
        pw.Text(
          'ORO POS - Sales report (INR)',
          style: const pw.TextStyle(fontSize: 20),
        ),
        pw.SizedBox(height: 16),
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
                  ].map((k) => money(r[k]).toStringAsFixed(2)),
                ],
              )
              .toList(),
        ),
      ],
    ),
  );
  await Printing.layoutPdf(
    name: 'ORO-sales-INR',
    onLayout: (_) async => doc.save(),
  );
}

Future<void> printOutletQr(String name, String pngBase64, String url) async {
  final doc = pw.Document();
  doc.addPage(
    pw.Page(
      build: (_) => pw.Center(
        child: pw.Column(
          mainAxisSize: pw.MainAxisSize.min,
          children: [
            pw.Text(
              name,
              style: pw.TextStyle(fontSize: 26, fontWeight: pw.FontWeight.bold),
            ),
            pw.SizedBox(height: 24),
            pw.Image(
              pw.MemoryImage(base64Decode(pngBase64)),
              width: 270,
              height: 270,
            ),
            pw.SizedBox(height: 20),
            pw.Text(
              'Scan to order - Pay cash at the outlet',
              style: const pw.TextStyle(fontSize: 18),
            ),
            pw.SizedBox(height: 12),
            pw.Text(url, style: const pw.TextStyle(fontSize: 9)),
            pw.SizedBox(height: 24),
            pw.Text('Powered by ORO POS'),
          ],
        ),
      ),
    ),
  );
  await Printing.layoutPdf(
    name: 'ORO-outlet-QR',
    onLayout: (_) async => doc.save(),
  );
}
