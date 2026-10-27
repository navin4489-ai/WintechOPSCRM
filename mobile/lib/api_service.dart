import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'models.dart';

class ApiException implements Exception {
  final String message;
  final int? status;
  const ApiException(this.message, [this.status]);
  @override
  String toString() => message;
}

class ApiService {
  static const baseUrl = String.fromEnvironment(
    'API_URL',
    defaultValue: 'https://the-bathany.onrender.com/api',
  );
  final http.Client client;
  final FlutterSecureStorage storage;
  String? _token;
  User? _user;
  int? _activeOutlet;
  String? _activeOutletName;
  String? get imageBase => Uri.parse(baseUrl).origin;
  String? imageUrl(dynamic value) => value is String && value.isNotEmpty
      ? Uri.parse(baseUrl).resolve(value).toString()
      : null;
  String get outletName => user?.roleName == 'Owner'
      ? (_activeOutletName ?? user?.outletName ?? 'Outlet workspace')
      : (user?.outletName ?? 'Outlet workspace');
  void Function()? onSessionExpired;
  ApiService({http.Client? client, FlutterSecureStorage? storage})
    : client = client ?? http.Client(),
      storage = storage ?? const FlutterSecureStorage();
  String? get token => _token;
  User? get user => _user;
  bool get isAuthenticated => _token != null && _user != null;
  int get outletId {
    final id = user?.roleName == 'Owner'
        ? (_activeOutlet ?? user?.outletId)
        : user?.outletId;
    if (id == null) {
      throw const ApiException(
        'Assign this account to an outlet before using POS.',
      );
    }
    return id;
  }

  bool can(String permission) =>
      user?.roleName == 'Owner' ||
      (user?.permissions.contains(permission) ?? false);
  bool get canManageCatalogue =>
      can('ManageSettings') || user?.roleName == 'StoreManager';

  Future<void> init() async {
    // Remove insecure credentials stored by earlier app versions. Require sign-in
    // rather than copying bearer credentials out of unencrypted preferences.
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('token');
    await prefs.remove('user');
    try {
      _token = await storage.read(key: 'oro_token');
      final saved = await storage.read(key: 'oro_user');
      if (_token == null || saved == null) {
        await logout();
        return;
      }
      final claims = jsonDecode(
        utf8.decode(
          base64Url.decode(base64Url.normalize(_token!.split('.')[1])),
        ),
      );
      if ((claims['exp'] as num) * 1000 <=
          DateTime.now().millisecondsSinceEpoch) {
        await logout();
        return;
      }
      _user = User.fromJson(jsonDecode(saved));
      if (_user?.roleName == 'Owner') {
        _activeOutlet = int.tryParse(
          await storage.read(key: 'oro_active_outlet') ?? '',
        );
        _activeOutletName = await storage.read(key: 'oro_active_outlet_name');
      }
    } catch (_) {
      await logout();
    }
  }

  Future<dynamic> request(
    String path, {
    String method = 'GET',
    Object? body,
  }) async {
    final uri = Uri.parse('$baseUrl$path');
    final headers = {
      'Content-Type': 'application/json',
      if (_token != null) 'Authorization': 'Bearer $_token',
    };
    try {
      final response = await (switch (method) {
        'POST' => client.post(
          uri,
          headers: headers,
          body: jsonEncode(body ?? {}),
        ),
        'PUT' => client.put(
          uri,
          headers: headers,
          body: jsonEncode(body ?? {}),
        ),
        'DELETE' => client.delete(uri, headers: headers),
        _ => client.get(uri, headers: headers),
      }).timeout(const Duration(seconds: 65));
      if (response.statusCode == 401) {
        if (path != '/auth/login') {
          await logout();
          onSessionExpired?.call();
        }
        throw const ApiException(
          'Session expired or invalid credentials. Please sign in.',
          401,
        );
      }
      if (response.statusCode == 403) {
        throw const ApiException(
          'Your role does not have permission for this action.',
          403,
        );
      }
      Map<String, dynamic>? envelope;
      try {
        envelope = jsonDecode(response.body) as Map<String, dynamic>;
      } catch (_) {
        /* Non-JSON upstream failure. */
      }
      if (response.statusCode < 200 ||
          response.statusCode >= 300 ||
          envelope?['success'] != true) {
        final errors = envelope?['errors'];
        throw ApiException(
          errors is List && errors.isNotEmpty
              ? errors.join('\n')
              : 'Request failed (${response.statusCode}). Refresh before retrying.',
          response.statusCode,
        );
      }
      return envelope?['data'];
    } on TimeoutException {
      throw const ApiException(
        'Connection timed out. A write may have completed: refresh before retrying.',
      );
    } on http.ClientException {
      throw const ApiException(
        'Cannot reach the server. Check your internet connection and refresh before retrying.',
      );
    }
  }

  Future<bool> login(String username, String password) async {
    final data = await request(
      '/auth/login',
      method: 'POST',
      body: {
        'username': username.trim(),
        'password': password,
        'deviceName': 'ORO Android',
        'stationType': 'POS',
      },
    );
    _token = data['token'];
    _user = User.fromJson(data['user']);
    await storage.write(key: 'oro_token', value: _token);
    await storage.write(key: 'oro_user', value: jsonEncode(data['user']));
    return true;
  }

  Future<void> logout() async {
    _token = null;
    _user = null;
    _activeOutlet = null;
    _activeOutletName = null;
    await storage.delete(key: 'oro_active_outlet');
    await storage.delete(key: 'oro_active_outlet_name');
    await storage.delete(key: 'oro_token');
    await storage.delete(key: 'oro_user');
  }

  Future<void> selectOutlet(int id, String name) async {
    if (user?.roleName != 'Owner') {
      throw const ApiException('Super Admin access required.');
    }
    _activeOutlet = id;
    _activeOutletName = name;
    await storage.write(key: 'oro_active_outlet', value: '$id');
    await storage.write(key: 'oro_active_outlet_name', value: name);
  }

  Future<void> uploadProductImage(int id, List<int> bytes) async {
    if (bytes.length > 1024 * 1024) {
      throw const ApiException('Choose an image up to 1 MB.');
    }
    final request =
        http.MultipartRequest('POST', Uri.parse('$baseUrl/products/$id/image'))
          ..headers['Authorization'] = 'Bearer $_token'
          ..files.add(
            http.MultipartFile.fromBytes(
              'file',
              bytes,
              filename: 'product-image',
            ),
          );
    final response = await http.Response.fromStream(
      await client.send(request).timeout(const Duration(seconds: 65)),
    );
    if (response.statusCode == 401) {
      await logout();
      onSessionExpired?.call();
      throw const ApiException('Session expired. Please sign in.');
    }
    if (response.statusCode != 200) {
      Map<String, dynamic>? body;
      try {
        body = jsonDecode(response.body);
      } catch (_) {}
      throw ApiException(
        (body?['errors'] as List?)?.join('\n') ?? 'Image upload failed.',
      );
    }
  }

  Future<List<Product>> getProducts(int outletId) async => ((await request(
    '/products?outletId=$outletId&availableOnly=true',
  )) as List).map((x) => Product.fromJson(x)).toList();
  Future<List<Table>> getTables(int outletId) async =>
      ((await request('/tables/outlet/$outletId')) as List)
          .map((x) => Table.fromJson(x))
          .toList();
  Future<Order> createOrder({
    required int outletId,
    int? tableId,
    required String serviceType,
  }) async => Order.fromJson(
    await request(
      '/orders',
      method: 'POST',
      body: {
        'outletId': outletId,
        'tableId': ?tableId,
        'serviceType': serviceType,
        'items': [],
      },
    ),
  );
  Future<Order> addOrderItem({
    required int orderId,
    required int productId,
    required int quantity,
  }) async => Order.fromJson(
    await request(
      '/orders/$orderId/items',
      method: 'POST',
      body: {
        'productId': productId,
        'quantity': quantity,
        'modifierOptionIds': [],
        'notes': '',
      },
    ),
  );
  Future<Order> processPayment({
    required int orderId,
    required String tenderType,
    required double amount,
  }) async => Order.fromJson(
    await request(
      '/orders/$orderId/payment',
      method: 'POST',
      body: {'orderId': orderId, 'tenderType': tenderType, 'amount': amount},
    ),
  );
  Future<List<KitchenTicket>> getKitchenTickets({int? outletId}) async =>
      ((await request(
        '/kitchentickets?outletId=${outletId ?? this.outletId}',
      )) as List).map((x) => KitchenTicket.fromJson(x)).toList();
  Future<void> updateTicketStatus(int ticketId, String status) async {
    await request(
      '/kitchentickets/$ticketId/status?status=$status',
      method: 'PUT',
    );
  }
}
