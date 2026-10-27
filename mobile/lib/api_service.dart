import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'models.dart';

class ApiService {
  // Production (Render) — ORO POS API
  static const String baseUrl = 'https://the-bathany.onrender.com/api';
  // Local development options:
  // static const String baseUrl = 'http://10.0.2.2:5000/api'; // Android emulator localhost
  // static const String baseUrl = 'http://localhost:5000/api'; // For physical device, use your PC's IP

  String? _token;
  User? _user;

  Future<void> init() async {
    final prefs = await SharedPreferences.getInstance();
    _token = prefs.getString('token');
    final userJson = prefs.getString('user');
    if (userJson != null) {
      _user = User.fromJson(jsonDecode(userJson));
    }
  }

  String? get token => _token;
  User? get user => _user;
  bool get isAuthenticated => _token != null;

  Map<String, String> get _headers => {
    'Content-Type': 'application/json',
    if (_token != null) 'Authorization': 'Bearer $_token',
  };

  Future<bool> login(String username, String password) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'username': username,
        'password': password,
        'deviceName': 'Flutter Mobile',
        'stationType': 'POS',
      }),
    );

    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        final loginData = data['data'];
        _token = loginData['token'];
        _user = User.fromJson(loginData['user']);

        final prefs = await SharedPreferences.getInstance();
        await prefs.setString('token', _token!);
        await prefs.setString('user', jsonEncode(loginData['user']));
        return true;
      }
    }
    return false;
  }

  Future<void> logout() async {
    _token = null;
    _user = null;
    final prefs = await SharedPreferences.getInstance();
    await prefs.clear();
  }

  Future<List<Product>> getProducts(int outletId) async {
    final response = await http.get(
      Uri.parse('$baseUrl/products?outletId=$outletId&availableOnly=true'),
      headers: _headers,
    );
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        return (data['data'] as List)
            .map((p) => Product.fromJson(p))
            .toList();
      }
    }
    throw Exception('Failed to load products');
  }

  Future<List<Table>> getTables(int outletId) async {
    final response = await http.get(
      Uri.parse('$baseUrl/tables/outlet/$outletId'),
      headers: _headers,
    );
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        return (data['data'] as List)
            .map((t) => Table.fromJson(t))
            .toList();
      }
    }
    throw Exception('Failed to load tables');
  }

  Future<Order> createOrder({
    required int outletId,
    required int tableId,
    required String serviceType,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/orders'),
      headers: _headers,
      body: jsonEncode({
        'outletId': outletId,
        'tableId': tableId,
        'serviceType': serviceType,
        'items': [],
      }),
    );
    if (response.statusCode == 200 || response.statusCode == 201) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        return Order.fromJson(data['data']);
      }
    }
    throw Exception('Failed to create order');
  }

  Future<Order> addOrderItem({
    required int orderId,
    required int productId,
    required int quantity,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/orders/$orderId/items'),
      headers: _headers,
      body: jsonEncode({
        'productId': productId,
        'quantity': quantity,
        'modifierOptionIds': [],
        'notes': '',
      }),
    );
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        return Order.fromJson(data['data']);
      }
    }
    throw Exception('Failed to add item');
  }

  Future<Order> processPayment({
    required int orderId,
    required String tenderType,
    required double amount,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/orders/$orderId/payment'),
      headers: _headers,
      body: jsonEncode({
        'orderId': orderId,
        'tenderType': tenderType,
        'amount': amount,
      }),
    );
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        return Order.fromJson(data['data']);
      }
    }
    throw Exception('Failed to process payment');
  }

  Future<List<KitchenTicket>> getKitchenTickets({int? outletId}) async {
    final url = Uri.parse('$baseUrl/kitchentickets')
        .replace(queryParameters: outletId != null ? {'outletId': outletId.toString()} : null);
    final response = await http.get(url, headers: _headers);
    if (response.statusCode == 200) {
      final data = jsonDecode(response.body);
      if (data['success'] == true) {
        return (data['data'] as List)
            .map((t) => KitchenTicket.fromJson(t))
            .toList();
      }
    }
    throw Exception('Failed to load kitchen tickets');
  }

  Future<void> updateTicketStatus(int ticketId, String status) async {
    final response = await http.put(
      Uri.parse('$baseUrl/kitchentickets/$ticketId/status?status=$status'),
      headers: _headers,
    );
    if (response.statusCode != 200) {
      throw Exception('Failed to update ticket status');
    }
  }
}
