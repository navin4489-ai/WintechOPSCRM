import 'package:flutter/material.dart';
import 'api_service.dart';
import 'models.dart';

class KDSHome extends StatefulWidget {
  const KDSHome({super.key});

  @override
  State<KDSHome> createState() => _KDSHomeState();
}

class _KDSHomeState extends State<KDSHome> {
  final api = ApiService();
  List<KitchenTicket> tickets = [];
  bool loading = true;

  @override
  void initState() {
    super.initState();
    _loadTickets();
  }

  Future<void> _loadTickets() async {
    final t = await api.getKitchenTickets(outletId: 1);
    if (mounted) {
      setState(() {
        tickets = t;
        loading = false;
      });
    }
  }

  Color _statusColor(String status) {
    switch (status) {
      case 'New': return Colors.grey;
      case 'Accepted': return Colors.blue;
      case 'InProgress': return Colors.orange;
      case 'Ready': return Colors.green;
      case 'Served': return Colors.grey.shade700;
      default: return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (loading) return const Scaffold(body: Center(child: CircularProgressIndicator()));

    if (tickets.isEmpty) {
      return const Scaffold(body: Center(child: Text('No kitchen tickets')));
    }

    return Scaffold(
      appBar: AppBar(title: const Text('ORO POS - Kitchen (KDS)')),
      body: RefreshIndicator(
        onRefresh: _loadTickets,
        child: GridView.builder(
          padding: const EdgeInsets.all(12),
          gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
            crossAxisCount: 2,
            childAspectRatio: 0.75,
            crossAxisSpacing: 12,
            mainAxisSpacing: 12,
          ),
          itemCount: tickets.length,
          itemBuilder: (context, index) {
            final t = tickets[index];
            final color = _statusColor(t.status);
            return Card(
              elevation: 4,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.1),
                      borderRadius: const BorderRadius.only(topLeft: Radius.circular(8), topRight: Radius.circular(8)),
                    ),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('#${t.orderNumber}', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
                        Text(t.createdAt.toString().substring(11, 16), style: TextStyle(fontSize: 12, color: Colors.grey.shade600)),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Table: ${t.tableNumber ?? 'Counter'}', style: const TextStyle(fontSize: 14)),
                        const SizedBox(height: 8),
                        const Text('Items:', style: TextStyle(fontWeight: FontWeight.bold)),
                        ...t.items.map((item) => Padding(
                          padding: const EdgeInsets.only(left: 8, bottom: 4),
                          child: Text('${item.productName} ×${item.quantity}', style: const TextStyle(fontSize: 13)),
                        )),
                        const SizedBox(height: 8),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                          decoration: BoxDecoration(color: color.withValues(alpha: 0.2), borderRadius: BorderRadius.circular(12)),
                          child: Text(t.status, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.bold)),
                        ),
                      ],
                    ),
                  ),
                  const Spacer(),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                    children: [
                      if (t.status == 'New')
                        Expanded(child: _ticketButton('Accept', () => _update(t.id, 'Accepted'), Colors.blue)),
                      if (t.status == 'Accepted')
                        Expanded(child: _ticketButton('Start', () => _update(t.id, 'InProgress'), Colors.orange)),
                      if (t.status == 'InProgress')
                        Expanded(child: _ticketButton('Ready', () => _update(t.id, 'Ready'), Colors.green)),
                      if (t.status == 'Ready')
                        Expanded(child: _ticketButton('Served', () => _update(t.id, 'Served'), Colors.teal)),
                    ],
                  ),
                  const Padding(padding: EdgeInsets.all(8)),
                ],
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _ticketButton(String label, VoidCallback onPressed, Color color) {
    return ElevatedButton.icon(
      onPressed: onPressed,
      icon: Icon(label == 'Accept' ? Icons.check : label == 'Start' ? Icons.play_circle : Icons.done),
      label: Text(label, style: const TextStyle(fontSize: 12)),
      style: ElevatedButton.styleFrom(backgroundColor: color, foregroundColor: Colors.white),
    );
  }

  Future<void> _update(int ticketId, String status) async {
    try {
      await api.updateTicketStatus(ticketId, status);
      _loadTickets();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Failed: $e')));
      }
    }
  }
}
