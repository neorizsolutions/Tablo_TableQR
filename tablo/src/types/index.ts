export type Category = string;

export interface MenuItem {
  id: string;
  name: string;
  description: string | null;
  price: number;
  image: string; // Used to hold local image import or URL
  category: Category;
  available: boolean;
  popular: boolean;
  special: boolean;
}

export type OrderStatus = "placed" | "accepted" | "preparing" | "ready" | "served" | "cancelled" | "rejected";

export interface OrderItem {
  id: string;
  menu_item_id: string | null;
  name: string;
  qty: number;
  price: number;
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
