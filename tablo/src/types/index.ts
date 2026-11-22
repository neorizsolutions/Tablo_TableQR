export type Category = string;

export interface MenuItem {
  id: string;
  name: string;
  description: string | null;
  price: number;
  image: string; // Used to hold local image import or URL
  category: Category;
  categoryName?: string;
  packagingFee?: number | null;
  available: boolean;
  popular: boolean;
  special: boolean;
  isRefillEligible?: boolean;
  refillMaxCount?: number;
  refillKitchenStationId?: string | null;
  kitchenStationId?: string | null;
  modifierGroups?: any[];
}

export type OrderStatus = "placed" | "accepted" | "preparing" | "ready" | "served" | "cancelled" | "rejected";

export interface OrderItem {
  id: string;
  menu_item_id: string | null;
  name: string;
  qty: number;
  price: number;
  release_state?: string;
  course_name?: string;
  modifiers_json?: string;
  is_refill?: boolean;
  refill_count?: number;
  cancelled_quantity?: number;
  is_cancelled?: boolean;
  packing_intent?: "DineIn" | "Takeaway" | string;
  notes?: string;
  special_instructions?: string;
}

export interface Order {
  group_label?: string;
  dining_group_id?: string;
  id: string;
  order_number: number;
  table_number: number;
  table_id: string;
  status: OrderStatus;
  subtotal: number;
  tax: number;
  discount: number;
  total: number;
  placed_at: string;
  items: OrderItem[];
  exception_reason?: string | null;
  channel?: string;
  order_channel?: string;
  delivery_provider?: string;
  external_order_reference?: string;
  daily_token_number?: number;
}

export interface Restaurant {
  id: string;
  slug: string;
  name: string;
  tagline: string | null;
}

export type ServiceRequestType = "CallWaiter" | "RequestBill";
export type ServiceRequestStatus = "Pending" | "Acknowledged" | "Completed" | "Cancelled";

export interface ServiceRequest {
  id: string;
  restaurantId: string;
  tableId: string;
  tableName: string;
  type: ServiceRequestType;
  status: ServiceRequestStatus;
  createdAt: string;
  updatedAt: string;
}
