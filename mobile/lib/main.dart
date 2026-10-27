import 'package:flutter/material.dart';
import 'api_service.dart';
import 'models.dart';
import 'currency.dart';
import 'waiter_home.dart';
import 'kds_home.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final api = ApiService();
  await api.init();
  runApp(MyApp(api: api));
}

class MyApp extends StatelessWidget {
  final ApiService? api;
  const MyApp({super.key, this.api});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ORO POS',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        brightness: Brightness.light,
        primaryColor: const Color(0xFF12265a),
        scaffoldBackgroundColor: const Color(0xFFeef1f7),
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF2456c4),
          primary: const Color(0xFF2456c4),
          onPrimary: Colors.white,
          secondary: const Color(0xFFf59e0b),
          surface: Colors.white,
          brightness: Brightness.light,
        ),
        appBarTheme: const AppBarTheme(
          centerTitle: false,
          elevation: 0,
          scrolledUnderElevation: 0,
          backgroundColor: Colors.white,
          foregroundColor: Color(0xFF0b1b3a),
          titleTextStyle: TextStyle(
            color: Color(0xFF0b1b3a),
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        cardTheme: const CardThemeData(
          color: Colors.white,
          elevation: 0,
          margin: EdgeInsets.zero,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.all(Radius.circular(14)),
            side: BorderSide(color: Color(0xFFe5e9f2)),
          ),
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF2456c4),
            foregroundColor: Colors.white,
            elevation: 0,
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          ),
        ),
        outlinedButtonTheme: OutlinedButtonThemeData(
          style: OutlinedButton.styleFrom(
            foregroundColor: const Color(0xFF2456c4),
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
            side: const BorderSide(color: Color(0xFFd3dae8)),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w600),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: const BorderSide(color: Color(0xFFd3dae8)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: const BorderSide(color: Color(0xFFd3dae8)),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(10),
            borderSide: const BorderSide(color: Color(0xFF2456c4), width: 1.6),
          ),
        ),
        snackBarTheme: SnackBarThemeData(
          behavior: SnackBarBehavior.floating,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
        ),
        dividerTheme: const DividerThemeData(color: Color(0xFFe5e9f2), space: 1, thickness: 1),
      ),
      home: api?.isAuthenticated ?? false ? const RoleSelector() : LoginScreen(api: api ?? ApiService()),
    );
  }
}

class RoleSelector extends StatelessWidget {
  const RoleSelector({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('ORO POS - Select Role')),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Text('Choose Your Role', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const WaiterHome())),
              icon: const Icon(Icons.restaurant),
              label: const Text('Waiter / Captain'),
              style: ElevatedButton.styleFrom(minimumSize: const Size(280, 50)),
            ),
            const SizedBox(height: 12),
            ElevatedButton.icon(
              onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const KDSHome())),
              icon: const Icon(Icons.kitchen),
              label: const Text('Kitchen (KDS)'),
              style: ElevatedButton.styleFrom(minimumSize: const Size(280, 50)),
            ),
            const SizedBox(height: 12),
            ElevatedButton.icon(
              onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const CustomerHome())),
              icon: const Icon(Icons.qr_code),
              label: const Text('Customer Order & Pay'),
              style: ElevatedButton.styleFrom(minimumSize: const Size(280, 50)),
            ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }
}

class LoginScreen extends StatelessWidget {
  final ApiService api;
  const LoginScreen({super.key, required this.api});

  @override
  Widget build(BuildContext context) {
    final userCtrl = TextEditingController(text: 'admin');
    final passCtrl = TextEditingController(text: 'Admin@123');
    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('ORO POS', style: TextStyle(fontSize: 32, fontWeight: FontWeight.bold, color: Color(0xFF17365D))),
                const SizedBox(height: 8),
                const Text('Sign In', style: TextStyle(fontSize: 18, color: Colors.grey)),
                const SizedBox(height: 24),
                TextField(controller: userCtrl, decoration: const InputDecoration(labelText: 'Username', border: OutlineInputBorder())),
                const SizedBox(height: 12),
                TextField(controller: passCtrl, decoration: const InputDecoration(labelText: 'Password', border: OutlineInputBorder()), obscureText: true),
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  height: 50,
                  child: ElevatedButton(
                    onPressed: () async {
                      final ok = await api.login(userCtrl.text, passCtrl.text);
                      if (ok && context.mounted) {
                        Navigator.pushReplacement(context, MaterialPageRoute(builder: (_) => const RoleSelector()));
                      } else if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Login failed')));
                      }
                    },
                    child: const Text('Sign In', style: TextStyle(fontSize: 16)),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class CustomerHome extends StatelessWidget {
  const CustomerHome({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('ORO POS - Customer Order & Pay')),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.qr_code, size: 80, color: Color(0xFF17365D)),
            const SizedBox(height: 24),
            const Text('Scan QR Code at Table', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            const Text('Order and pay from your mobile device', style: TextStyle(fontSize: 14, color: Colors.grey)),
            const SizedBox(height: 32),
            SizedBox(
              width: 280,
              height: 50,
              child: ElevatedButton.icon(
                onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const CustomerMenu())),
                icon: const Icon(Icons.restaurant_menu),
                label: const Text('Browse Menu'),
              ),
            ),
            const SizedBox(height: 12),
            SizedBox(
              width: 280,
              height: 50,
              child: OutlinedButton.icon(
                onPressed: () => Navigator.push(context, MaterialPageRoute(builder: (_) => const CustomerOrderStatus())),
                icon: const Icon(Icons.receipt_long),
                label: const Text('Check Order Status'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class CustomerMenu extends StatelessWidget {
  const CustomerMenu({super.key});

  @override
  Widget build(BuildContext context) {
    final api = ApiService();
    return Scaffold(
      appBar: AppBar(title: const Text('Menu')),
      body: FutureBuilder<List<Product>>(
        future: api.getProducts(1),
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) return const Center(child: CircularProgressIndicator());
          if (snapshot.hasError) return Center(child: Text('Error: ${snapshot.error}'));
          final products = snapshot.data ?? [];
          return ListView.builder(
            itemCount: products.length,
            itemBuilder: (context, index) {
              final p = products[index];
              return ListTile(
                leading: const Icon(Icons.fastfood),
                title: Text(p.name),
                subtitle: Text(formatInr(p.price), style: const TextStyle(fontWeight: FontWeight.w600, color: Color(0xFF16a34a))),
                trailing: ElevatedButton(
                  onPressed: () {},
                  child: const Text('Add'),
                ),
              );
            },
          );
        },
      ),
    );
  }
}

class CustomerOrderStatus extends StatelessWidget {
  const CustomerOrderStatus({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Order Status')),
      body: const Center(child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.receipt_long, size: 64, color: Color(0xFF17365D)),
          SizedBox(height: 16),
          Text('Enter your order number to track status', style: TextStyle(fontSize: 16)),
          SizedBox(height: 16),
          SizedBox(width: 200, child: TextField(decoration: InputDecoration(labelText: 'Order Number', border: OutlineInputBorder()))),
        ],
      )),
    );
  }
}
