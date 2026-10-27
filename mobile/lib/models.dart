class Product {
  final int id;
  final String name;
  final String sku;
  final double price;
  final bool isAvailable;
  final String? categoryName;

  Product({
    required this.id,
    required this.name,
    required this.sku,
    required this.price,
    required this.isAvailable,
    this.categoryName,
  });

  factory Product.fromJson(Map<String, dynamic> json) {
    return Product(
      id: json['id'],
      name: json['name'],
      sku: json['sku'],
      price: (json['price'] as num).toDouble(),
      isAvailable: json['isAvailable'] ?? true,
      categoryName: json['categoryName'],
    );
  }
}

class Table {
  final int id;
  final String tableNumber;
  final String status;
  final int capacity;
  final String? zoneName;
  final bool hasOpenOrder;

  Table({
    required this.id,
    required this.tableNumber,
    required this.status,
    required this.capacity,
    this.zoneName,
    this.hasOpenOrder = false,
  });

  factory Table.fromJson(Map<String, dynamic> json) {
    return Table(
      id: json['id'],
      tableNumber: json['tableNumber'],
      status: json['status'],
      capacity: json['capacity'],
      zoneName: json['zoneName'],
      hasOpenOrder: json['hasOpenOrder'] ?? false,
    );
  }
}

class OrderItem {
  final int id;
  final String productName;
  final int quantity;
  final double unitPrice;
  final double lineTotal;
  final String kitchenStation;
  final String status;

  OrderItem({
    required this.id,
    required this.productName,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
    required this.kitchenStation,
    required this.status,
  });

  factory OrderItem.fromJson(Map<String, dynamic> json) {
    return OrderItem(
      id: json['id'],
      productName: json['productName'],
      quantity: json['quantity'],
      unitPrice: (json['unitPrice'] as num).toDouble(),
      lineTotal: (json['lineTotal'] as num).toDouble(),
      kitchenStation: json['kitchenStation'] ?? '',
      status: json['status'] ?? '',
    );
  }
}

class Order {
  final int id;
  final String orderNumber;
  final String status;
  final String serviceType;
  final String? tableNumber;
  final double subtotal;
  final double totalAmount;
  final List<OrderItem> orderItems;

  Order({
    required this.id,
    required this.orderNumber,
    required this.status,
    required this.serviceType,
    this.tableNumber,
    required this.subtotal,
    required this.totalAmount,
    required this.orderItems,
  });

  factory Order.fromJson(Map<String, dynamic> json) {
    return Order(
      id: json['id'],
      orderNumber: json['orderNumber'],
      status: json['status'],
      serviceType: json['serviceType'],
      tableNumber: json['tableNumber'],
      subtotal: (json['subtotal'] as num).toDouble(),
      totalAmount: (json['totalAmount'] as num).toDouble(),
      orderItems: (json['orderItems'] as List)
          .map((i) => OrderItem.fromJson(i))
          .toList(),
    );
  }
}

class KitchenTicket {
  final int id;
  final String orderNumber;
  final String? tableNumber;
  final String stationName;
  final String status;
  final int priority;
  final DateTime createdAt;
  final List<KitchenTicketItem> items;

  KitchenTicket({
    required this.id,
    required this.orderNumber,
    this.tableNumber,
    required this.stationName,
    required this.status,
    required this.priority,
    required this.createdAt,
    required this.items,
  });

  factory KitchenTicket.fromJson(Map<String, dynamic> json) {
    return KitchenTicket(
      id: json['id'],
      orderNumber: json['orderNumber'],
      tableNumber: json['tableNumber'],
      stationName: json['stationName'],
      status: json['status'],
      priority: json['priority'],
      createdAt: DateTime.parse(json['createdAt']),
      items: (json['items'] as List)
          .map((i) => KitchenTicketItem.fromJson(i))
          .toList(),
    );
  }
}

class KitchenTicketItem {
  final String productName;
  final int quantity;
  final String? modifiers;
  final String? notes;
  final String status;

  KitchenTicketItem({
    required this.productName,
    required this.quantity,
    this.modifiers,
    this.notes,
    required this.status,
  });

  factory KitchenTicketItem.fromJson(Map<String, dynamic> json) {
    return KitchenTicketItem(
      productName: json['productName'],
      quantity: json['quantity'],
      modifiers: json['modifiers'],
      notes: json['notes'],
      status: json['status'],
    );
  }
}

class User {
  final int id;
  final String username;
  final String roleName;
  final List<String> permissions;
  final int? outletId;
  final String? outletName;

  User({
    required this.id,
    required this.username,
    required this.roleName,
    required this.permissions,
    this.outletId,
    this.outletName,
  });

  factory User.fromJson(Map<String, dynamic> json) {
    return User(
      id: json['id'],
      username: json['username'],
      roleName: json['roleName'] ?? '',
      permissions: List<String>.from(json['permissions'] ?? []),
      outletId: json['outletId'],
      outletName: json['outletName'],
    );
  }
}
