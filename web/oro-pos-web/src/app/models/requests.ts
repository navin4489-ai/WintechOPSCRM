export interface OrderItemRequest {
  productId: number;
  quantity: number;
  notes?: string;
  modifierOptionIds: number[];
}

export interface CreateOrderRequest {
  outletId: number;
  tableId?: number;
  serviceType: string;
  items: OrderItemRequest[];
  notes?: string;
}

export interface CreateCategoryRequest {
  outletId: number;
  name: string;
  sortOrder: number;
}

export interface CreateProductRequest {
  outletId: number;
  categoryId?: number;
  sku: string;
  barcode?: string;
  name: string;
  description?: string;
  price: number;
  taxClass: string;
  isAvailable: boolean;
  sortOrder: number;
  modifierGroupIds: number[];
}

import { ModifierOptionDto } from './dto';

export interface CreateModifierGroupRequest {
  outletId: number;
  name: string;
  isRequired: boolean;
  minSelections: number;
  maxSelections: number;
  options: ModifierOptionDto[];
}

export interface CreateTableRequest {
  outletId: number;
  tableNumber: string;
  capacity: number;
  floorPlanX?: number;
  floorPlanY?: number;
}

export interface CreateZoneRequest {
  outletId: number;
  name: string;
  floorPlanData?: string;
}
