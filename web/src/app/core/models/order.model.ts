export interface Order {
  id: string;
  branchId: string;
  clientUserId: string;
  status: string;
  priceTotal: number;
  totalItems: number;
  currency: string;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  items?: OrderItem[];
}

export interface OrderItem {
  id: string;
  orderId: string;
  itemType: string;
  productId: string | null;
  bundleId: string | null;
  quantity: number;
  unitPrice: number;
  subtotal: number;
  currency: string;
}

export interface CreateOrderRequest {
  branch_id: string;
  client_user_id: string;
  notes: string;
  items: CreateOrderItem[];
}

export interface CreateOrderItem {
  item_type: string;
  product_id: string;
  bundle_id: string;
  quantity: number;
  unit_price: number;
}

export interface UpdateOrderRequest {
  notes: string;
  items: CreateOrderItem[];
}

export interface StatusChangeRequest {
  to_status: string;
  notes: string;
}

export interface StatusHistoryEntry {
  id: string;
  orderId: string;
  fromStatus: string | null;
  toStatus: string;
  changedByUserId: string;
  notes: string | null;
  createdAtUtc: string;
}

export interface OrderFilter {
  branch_id?: string;
  client_user_id?: string;
  status?: string;
  limit?: number;
  offset?: number;
}
