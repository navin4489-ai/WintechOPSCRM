import 'package:flutter/material.dart' hide Table;
import 'api_service.dart';
import 'models.dart';

class WaiterHome extends StatefulWidget {
  const WaiterHome({super.key});

  @override
  State<WaiterHome> createState() => _WaiterHomeState();
}

class _WaiterHomeState extends State<WaiterHome> {
  final api = ApiService();
  List<Table> tables = [];
  List<Product> products = [];
  Order? currentOrder;
  bool loading = true;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    final outletId = 1;
    final t = await api.getTables(outletId);
    final p = await api.getProducts(outletId);
    if (mounted) {
      setState(() {
        tables = t;
        products = p;
        loading = false;
      });
    }
  }

  void _selectTable(Table table) {
    setState(() {
      tables = tables.map((t) {
        if (t.id == table.id) {
          return Table(
            id: t.id,
            tableNumber: t.tableNumber,
            status: 'Selected',
            capacity: t.capacity,
            zoneName: t.zoneName,
            hasOpenOrder: t.hasOpenOrder,
          );
        }
        return Table(
          id: t.id,
          tableNumber: t.tableNumber,
          status: t.status == 'Selected' ? 'Free' : t.status,
          capacity: t.capacity,
          zoneName: t.zoneName,
          hasOpenOrder: t.hasOpenOrder,
        );
      }).toList();
    });
    _createOrderForTable(table.id);
  }

  Future<void> _createOrderForTable(int tableId) async {
    try {
      final order = await api.createOrder(outletId: 1, tableId: tableId, serviceType: 'DineIn');
      if (mounted) {
        setState(() => currentOrder = order);
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed to create order: $e')));
      }
    }
  }

  void _addItem(Product product) {
    if (currentOrder == null) return;
    api.addOrderItem(orderId: currentOrder!.id, productId: product.id, quantity: 1).then((order) {
      if (mounted) {
        setState(() => currentOrder = order);
      }
    });
  }

  Future<void> _processPayment() async {
    if (currentOrder == null) return;
    final amountCtrl = TextEditingController(text: currentOrder!.totalAmount.toStringAsFixed(2));
    await showDialog(
      context: context,
      builder: (_) => AlertDialog(
        title: const Text('Process Payment'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(controller: amountCtrl, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Amount')),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              value: 'Cash',
              items: ['Cash', 'Card', 'Digital Wallet'].map((t) => DropdownMenuItem(value: t, child: Text(t))).toList(),
              onChanged: (_) {},
              decoration: const InputDecoration(labelText: 'Tender Type'),
            ),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancel')),
          ElevatedButton(
            onPressed: () async {
              final amt = double.tryParse(amountCtrl.text) ?? 0;
              try {
                await api.processPayment(orderId: currentOrder!.id, tenderType: 'Cash', amount: amt);
                if (mounted) {
                  Navigator.pop(context);
                  setState(() => currentOrder = null);
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Payment processed!')));
                }
              } catch (e) {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Payment failed: $e')));
                }
              }
            },
            child: const Text('Confirm'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    if (loading) return const Scaffold(body: Center(child: CircularProgressIndicator()));
    return Scaffold(
      appBar: AppBar(title: const Text('ORO POS - Waiter')),
      body: Row(
        children: [
          SizedBox(
            width: 300,
            child: Column(
              children: [
                const Padding(
                  padding: EdgeInsets.all(12),
                  child: Text('Tables', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                ),
                Expanded(
                  child: GridView.builder(
                    padding: const EdgeInsets.all(8),
                    gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                      crossAxisCount: 2,
                      childAspectRatio: 1.2,
                      crossAxisSpacing: 8,
                      mainAxisSpacing: 8,
                    ),
                    itemCount: tables.length,
                    itemBuilder: (context, index) {
                      final t = tables[index];
                      final isSelected = t.status == 'Selected';
                      return GestureDetector(
                        onTap: () => _selectTable(t),
                        child: Container(
                          decoration: BoxDecoration(
                            color: isSelected
                                ? Colors.blue.shade50
                                : (t.status == 'Occupied' ? Colors.red.shade50 : Colors.green.shade50),
                            border: Border.all(
                              color: isSelected ? Colors.blue : (t.status == 'Occupied' ? Colors.red : Colors.green),
                              width: 2,
                            ),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Text(t.tableNumber, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
                              const SizedBox(height: 4),
                              Text(t.status, style: TextStyle(fontSize: 10, color: Colors.grey.shade700)),
                              Text('${t.capacity} seats', style: TextStyle(fontSize: 10, color: Colors.grey.shade600)),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
                ),
              ],
            ),
          ),
          Expanded(
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: Row(
                    children: [
                      Text('Order: ${currentOrder?.orderNumber ?? 'Select a table'}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                      const Spacer(),
                      if (currentOrder != null)
                        ElevatedButton.icon(
                          onPressed: _processPayment,
                          icon: const Icon(Icons.payment),
                          label: const Text('Pay'),
                        ),
                    ],
                  ),
                ),
                Expanded(
                  child: currentOrder == null
                      ? const Center(child: Text('Select a table to start an order'))
                      : ListView.builder(
                          itemCount: currentOrder!.orderItems.length,
                          itemBuilder: (context, index) {
                            final item = currentOrder!.orderItems[index];
                            return ListTile(
                              leading: CircleAvatar(child: Text('${item.quantity}')),
                              title: Text(item.productName),
                              subtitle: Text('\$${item.lineTotal.toStringAsFixed(2)}'),
                            );
                          },
                        ),
                ),
                if (currentOrder != null)
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: Colors.grey.shade100,
                      border: Border(top: BorderSide(color: Colors.grey.shade300)),
                    ),
                    child: Column(
                      children: [
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text('Subtotal:', style: TextStyle(fontWeight: FontWeight.bold)),
                            Text('\$${currentOrder!.subtotal.toStringAsFixed(2)}'),
                          ],
                        ),
                        Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            const Text('Total:', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                            Text('\$${currentOrder!.totalAmount.toStringAsFixed(2)}', style: const TextStyle(fontSize: 18, color: Color(0xFF17365D), fontWeight: FontWeight.bold)),
                          ],
                        ),
                      ],
                    ),
                  ),
              ],
            ),
          ),
          SizedBox(
            width: 320,
            child: Column(
              children: [
                const Padding(
                  padding: EdgeInsets.all(12),
                  child: Text('Menu', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                ),
                Expanded(
                  child: ListView.builder(
                    itemCount: products.length,
                    itemBuilder: (context, index) {
                      final p = products[index];
                      return Card(
                        child: ListTile(
                          title: Text(p.name),
                          subtitle: Text('\$${p.price.toStringAsFixed(2)}  |  ${p.sku}'),
                          trailing: ElevatedButton(
                            onPressed: () => _addItem(p),
                            child: const Text('Add'),
                          ),
                        ),
                      );
                    },
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
