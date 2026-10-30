import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'api_service.dart';
import 'brand.dart';
import 'kitchen_bell.dart';
import 'operations.dart';
import 'pos_screen.dart';
import 'ui_helpers.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final api = ApiService();
  await api.init();
  runApp(MyApp(api: api));
}

class MyApp extends StatefulWidget {
  final ApiService? api;
  const MyApp({super.key, this.api});
  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  late final ApiService api = widget.api ?? ApiService();
  final navigatorKey = GlobalKey<NavigatorState>();
  @override
  void initState() {
    super.initState();
    api.onSessionExpired = () {
      navigatorKey.currentState?.popUntil((route) => route.isFirst);
      if (mounted) setState(() {});
    };
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
    navigatorKey: navigatorKey,
    title: 'Wintech POS',
    debugShowCheckedModeBanner: false,
    theme: ThemeData(
      useMaterial3: true,
      colorScheme: ColorScheme.fromSeed(
        seedColor: navy,
        primary: navy,
        secondary: gold,
        surface: Colors.white,
      ),
      scaffoldBackgroundColor: canvas,
      appBarTheme: const AppBarTheme(
        backgroundColor: canvas,
        foregroundColor: navy,
        centerTitle: false,
        elevation: 0,
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(20),
          side: const BorderSide(color: Color(0xffe3e8f1)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(14),
          borderSide: const BorderSide(color: Color(0xffd5deeb)),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(44, 50),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(14),
          ),
        ),
      ),
      snackBarTheme: const SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
      ),
    ),
    home: api.isAuthenticated
        ? OperationsHome(api: api, onLogout: () => setState(() {}))
        : SignIn(api: api, onSuccess: () => setState(() {})),
  );
}

class SignIn extends StatefulWidget {
  final ApiService api;
  final VoidCallback onSuccess;
  const SignIn({super.key, required this.api, required this.onSuccess});
  @override
  State<SignIn> createState() => _SignInState();
}

class _SignInState extends State<SignIn> {
  final username = TextEditingController();
  final password = TextEditingController();
  final form = GlobalKey<FormState>();
  bool busy = false, obscure = true;
  String? error;
  @override
  void dispose() {
    username.dispose();
    password.dispose();
    super.dispose();
  }

  Future<void> login() async {
    if (busy || !form.currentState!.validate()) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.api.login(username.text, password.text);
      if (mounted) widget.onSuccess();
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [navy, Color(0xff234575)],
        ),
      ),
      child: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 430),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Center(child: WintechLogo(size: 76)),
                  const SizedBox(height: 20),
                  const Text(
                    'Wintech POS',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 32,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 4,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Your floor. Your kitchen. In sync.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Colors.white70),
                  ),
                  const SizedBox(height: 36),
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(24),
                      child: Form(
                        key: form,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const Text(
                              'Welcome back',
                              style: TextStyle(
                                fontSize: 24,
                                fontWeight: FontWeight.w800,
                                color: navy,
                              ),
                            ),
                            const SizedBox(height: 6),
                            const Text('Sign in to your outlet workspace'),
                            const SizedBox(height: 24),
                            TextFormField(
                              controller: username,
                              enabled: !busy,
                              autofillHints: const [AutofillHints.username],
                              decoration: const InputDecoration(
                                labelText: 'Username or email',
                                prefixIcon: Icon(Icons.person_outline),
                              ),
                              validator: (v) => v == null || v.trim().isEmpty
                                  ? 'Enter your username or email'
                                  : null,
                            ),
                            const SizedBox(height: 16),
                            TextFormField(
                              controller: password,
                              enabled: !busy,
                              obscureText: obscure,
                              autofillHints: const [AutofillHints.password],
                              decoration: InputDecoration(
                                labelText: 'Password',
                                prefixIcon: const Icon(Icons.lock_outline),
                                suffixIcon: IconButton(
                                  tooltip: obscure
                                      ? 'Show password'
                                      : 'Hide password',
                                  icon: Icon(
                                    obscure
                                        ? Icons.visibility_outlined
                                        : Icons.visibility_off_outlined,
                                  ),
                                  onPressed: () =>
                                      setState(() => obscure = !obscure),
                                ),
                              ),
                              validator: (v) => v == null || v.isEmpty
                                  ? 'Enter your password'
                                  : null,
                              onFieldSubmitted: (_) => login(),
                            ),
                            if (error != null)
                              Padding(
                                padding: const EdgeInsets.symmetric(
                                  vertical: 12,
                                ),
                                child: Text(
                                  error!,
                                  style: const TextStyle(color: Colors.red),
                                ),
                              ),
                            const SizedBox(height: 24),
                            FilledButton(
                              onPressed: busy ? null : login,
                              child: busy
                                  ? const SizedBox(
                                      width: 22,
                                      height: 22,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  : const Text('Sign in securely'),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 24),
                  const Text(
                    'Encrypted session • INR billing • Connected workspace',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: Colors.white60, fontSize: 12),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class OperationsHome extends StatefulWidget {
  final ApiService api;
  final VoidCallback onLogout;
  const OperationsHome({super.key, required this.api, required this.onLogout});
  @override
  State<OperationsHome> createState() => _OperationsHomeState();
}

class _OperationsHomeState extends State<OperationsHome> {
  String current = 'Overview';

  /// Kitchen bell for the whole app, not just the Kitchen screen: a ticket
  /// that arrives while staff are on the POS or Orders screen still rings.
  late final KitchenAlertService kitchenAlert = KitchenAlertService(
    fetchTickets: () async {
      final rows = await widget.api.request(
        '/kitchentickets?outletId=${widget.api.outletId}',
      );
      return rows is List ? rows : const [];
    },
    secureStorage: const FlutterSecureStorage(),
  );

  /// The bell only makes sense for accounts tied to an outlet with a kitchen.
  bool get kitchenBellAllowed {
    try {
      widget.api.outletId;
      return true;
    } catch (_) {
      return false;
    }
  }

  @override
  void initState() {
    super.initState();
    if (kitchenBellAllowed) kitchenAlert.start();
  }

  @override
  void dispose() {
    kitchenAlert.disposeService();
    super.dispose();
  }

  Future<void> changePassword() async {
    final values = await askForm(
      context,
      'Change password',
      [
        const FormFieldSpec(
          'currentPassword',
          'Current password',
          secret: true,
        ),
        const FormFieldSpec(
          'newPassword',
          'New password (12+ characters)',
          secret: true,
        ),
      ],
      warning: 'Use upper/lowercase letters and a number. This signs out every session on this account.',
    );
    if (values == null) return;
    try {
      await widget.api.request('/auth/password', method: 'POST', body: values);
      await widget.api.logout();
      widget.onLogout();
    } catch (e) {
      if (mounted) notify(context, e);
    }
  }

  List<(String, IconData)> get modules => [
    ('Overview', Icons.space_dashboard_outlined),
    if (widget.api.can('CreateOrder')) ('Point of sale', Icons.point_of_sale),
    ('Orders', Icons.receipt_long_outlined),
    ('Kitchen', Icons.soup_kitchen_outlined),
    ('Shifts', Icons.schedule),
    ('Tables', Icons.table_restaurant_outlined),
    ('Catalogue', Icons.restaurant_menu),
    if (widget.api.user?.roleName == 'Owner')
      ('Super Admin', Icons.admin_panel_settings_outlined),
    if (widget.api.user?.roleName == 'StoreManager')
      ('My Outlet', Icons.storefront_outlined),
    if (widget.api.can('ManageInventory'))
      ('Inventory', Icons.inventory_2_outlined),
    if (widget.api.can('ViewReports')) ...[
      ('Reports', Icons.bar_chart),
      ('Audit log', Icons.policy_outlined),
    ],
    if (widget.api.can('ManageUsers')) ('Staff', Icons.group_outlined),
  ];
  Future<void> logout() async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Sign out?'),
        content: const Text('Your orders remain safely on the server.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Sign out'),
          ),
        ],
      ),
    );
    if (confirm != true) return;
    await widget.api.logout();
    widget.onLogout();
  }

  Widget navigation(bool wide) => Column(
    children: [
      const Padding(
        padding: EdgeInsets.all(24),
        child: Row(
          children: [
            WintechLogo(size: 42),
            SizedBox(width: 12),
            Expanded(
              child: FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text(
                  'Wintech POS',
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
                ),
              ),
            ),
          ],
        ),
      ),
      Expanded(
        child: ListView(
          children: modules
              .map(
                (m) => Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 2,
                  ),
                  child: ListTile(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(12),
                    ),
                    selectedTileColor: const Color(0xffe7edf7),
                    selected: current == m.$1,
                    leading: Icon(m.$2),
                    title: Text(m.$1),
                    onTap: () {
                      setState(() => current = m.$1);
                      if (!wide) Navigator.pop(context);
                    },
                  ),
                ),
              )
              .toList(),
        ),
      ),
      ListTile(
        leading: const Icon(Icons.password),
        title: const Text('Change password'),
        onTap: changePassword,
      ),
      ListTile(
        leading: const Icon(Icons.logout),
        title: const Text('Sign out'),
        subtitle: Text(
          '${widget.api.user?.username} · ${widget.api.user?.roleName}',
        ),
        onTap: logout,
      ),
      const SizedBox(height: 12),
    ],
  );
  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final wide = constraints.maxWidth >= 1000;
      final body = current == 'Point of sale'
          ? PosScreen(key: ValueKey(current), api: widget.api)
          : OperationsScreen(
              key: ValueKey(current),
              api: widget.api,
              module: current,
              onNavigate: (value) => setState(() => current = value),
            );
      return Scaffold(
        drawer: wide ? null : Drawer(child: SafeArea(child: navigation(false))),
        appBar: AppBar(
          title: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                current,
                style: const TextStyle(fontWeight: FontWeight.w800),
              ),
              Text(widget.api.outletName, style: const TextStyle(fontSize: 12)),
            ],
          ),
          actions: [
            IconButton(
              tooltip: 'Cash shifts',
              onPressed: () => setState(() => current = 'Shifts'),
              icon: const Icon(Icons.schedule),
            ),
            const SizedBox(width: 12),
          ],
        ),
        body: Column(
          children: [
            // App-wide kitchen bell: visible on every screen, not only the
            // Kitchen module.
            ListenableBuilder(
              listenable: kitchenAlert,
              builder: (context, _) => kitchenBanner(),
            ),
            Expanded(
              child: Row(
                children: [
                  if (wide) SizedBox(width: 250, child: navigation(true)),
                  Expanded(child: SafeArea(child: body)),
                ],
              ),
            ),
          ],
        ),
      );
    },
  );

  Widget kitchenBanner() {
    final alert = kitchenAlert;
    if (!alert.running) return const SizedBox.shrink();
    // Idle outside the Kitchen screen: no need to take space from the POS.
    if (alert.pending == 0 && current != 'Kitchen') return const SizedBox.shrink();
    final emergency = alert.emergency;
    final waiting = alert.pending > 0;
    final bg = emergency
        ? const Color(0xffb3261e)
        : waiting
        ? const Color(0xff8a5300)
        : const Color(0xff2f4a7a);
    final waited = Duration(seconds: alert.oldestWaitSeconds);
    final waitLabel = waited.inMinutes > 0
        ? '${waited.inMinutes}m ${waited.inSeconds % 60}s'
        : '${waited.inSeconds}s';
    return Material(
      color: bg,
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          child: Row(
            children: [
              Icon(
                emergency
                    ? Icons.warning_amber_rounded
                    : waiting
                    ? Icons.notifications_active
                    : Icons.notifications_none,
                color: Colors.white,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      emergency
                          ? 'KITCHEN EMERGENCY — ${alert.pending} order${alert.pending == 1 ? '' : 's'} waiting'
                          : waiting
                          ? '${alert.pending} order${alert.pending == 1 ? '' : 's'} in the kitchen — bell ${alert.muted ? 'muted' : 'ringing'}'
                          : 'Kitchen bell ready — no orders waiting',
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    Text(
                      waiting
                          ? 'Oldest waiting $waitLabel. The bell stops once staff accept the order.'
                          : 'New orders ring automatically, even on other screens.',
                      style: const TextStyle(color: Colors.white70, fontSize: 12),
                    ),
                  ],
                ),
              ),
              if (current == 'Kitchen') ...[
                TextButton(
                  onPressed: () => kitchenAlert.testRing(),
                  child: const Text(
                    'Test bell',
                    style: TextStyle(color: Colors.white),
                  ),
                ),
                TextButton(
                  onPressed: () => kitchenAlert.testRing(emergency: true),
                  child: const Text(
                    'Test emergency',
                    style: TextStyle(color: Colors.white),
                  ),
                ),
              ],
              TextButton(
                onPressed: () => kitchenAlert.setMuted(!alert.muted),
                child: Text(
                  alert.muted ? 'Unmute' : 'Mute',
                  style: const TextStyle(color: Colors.white),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
