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

export type OrderStatus = "placed" | "accepted" | "preparing" | "ready" | "served" | "cancelled";

export interface OrderItem {
  id: string;
  menu_item_id: string | null;
  name: string;
  qty: number;
  price: number;
}

export interface Order {
  id: string;
  order_number: number;
  table_number: number;
  table_id: string;
  status: OrderStatus;
  total: number;
  placed_at: string;
  items: OrderItem[];
}

export interface Restaurant {
  id: string;
  slug: string;
  name: string;
  tagline: string | null;
}
