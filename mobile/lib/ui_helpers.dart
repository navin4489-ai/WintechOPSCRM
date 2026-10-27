import 'package:flutter/material.dart';

import 'api_service.dart';
export 'documents.dart';

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
