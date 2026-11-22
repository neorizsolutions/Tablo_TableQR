import bruschetta from "@/assets/food-bruschetta.jpg";
import risotto from "@/assets/food-risotto.jpg";
import salmon from "@/assets/food-salmon.jpg";
import smoothie from "@/assets/food-smoothie.jpg";
import cake from "@/assets/food-cake.jpg";
import salad from "@/assets/food-salad.jpg";
import pizza from "@/assets/food-pizza.jpg";
import lemonade from "@/assets/food-lemonade.jpg";

export type Category = "Starters" | "Main Course" | "Drinks" | "Desserts";

export interface MenuItem {
  id: string;
  name: string;
  description: string;
  price: number;
  image: string;
  category: Category;
  available: boolean;
  popular?: boolean;
  special?: boolean;
}

export const restaurant = {
  name: "Saffron & Sage",
  tagline: "Modern Indian Kitchen",
  table: 12,
};

export const categories: Category[] = ["Starters", "Main Course", "Drinks", "Desserts"];

export const menu: MenuItem[] = [
  { id: "m1", name: "Tomato Basil Bruschetta", description: "Toasted sourdough, heirloom tomato, basil, parmesan", price: 240, image: bruschetta, category: "Starters", available: true, popular: true },
  { id: "m2", name: "Avocado Garden Bowl", description: "Avocado, cherry tomato, basil, olive oil", price: 280, image: salad, category: "Starters", available: true },
  { id: "m3", name: "Wild Mushroom Risotto", description: "Arborio rice, porcini, parmesan, parsley", price: 420, image: risotto, category: "Main Course", available: true, special: true },
  { id: "m4", name: "Grilled Atlantic Salmon", description: "Lemon butter, asparagus, dill", price: 580, image: salmon, category: "Main Course", available: true, popular: true },
  { id: "m5", name: "Margherita Wood-Fired Pizza", description: "San Marzano, fior di latte, basil", price: 380, image: pizza, category: "Main Course", available: false },
  { id: "m6", name: "Mint Lemonade", description: "Fresh lime, mint, palm sugar, sparkling water", price: 150, image: lemonade, category: "Drinks", available: true },
  { id: "m7", name: "Garden Green Smoothie", description: "Spinach, apple, ginger, lime, mint", price: 180, image: smoothie, category: "Drinks", available: true, special: true },
  { id: "m8", name: "Molten Chocolate Cake", description: "Warm chocolate, vanilla bean ice cream", price: 260, image: cake, category: "Desserts", available: true, popular: true },
];

export type OrderStatus = "placed" | "accepted" | "preparing" | "ready" | "served" | "cancelled" | "rejected";

export interface OrderItem {
  itemId: string;
  name: string;
  qty: number;
  price: number;
}

export interface Order {
  id: string;
  table: number;
  items: OrderItem[];
  total: number;
  status: OrderStatus;
  placedAt: string;
}

export const seedOrders: Order[] = [
  {
    id: "ORD-2041",
    table: 4,
    items: [
      { itemId: "m4", name: "Grilled Atlantic Salmon", qty: 2, price: 580 },
      { itemId: "m6", name: "Mint Lemonade", qty: 2, price: 150 },
    ],
    total: 1460,
    status: "preparing",
    placedAt: new Date(Date.now() - 6 * 60_000).toISOString(),
  },
  {
    id: "ORD-2042",
    table: 9,
    items: [
      { itemId: "m3", name: "Wild Mushroom Risotto", qty: 1, price: 420 },
      { itemId: "m8", name: "Molten Chocolate Cake", qty: 1, price: 260 },
    ],
    total: 680,
    status: "placed",
    placedAt: new Date(Date.now() - 2 * 60_000).toISOString(),
  },
  {
    id: "ORD-2043",
    table: 12,
    items: [
      { itemId: "m1", name: "Tomato Basil Bruschetta", qty: 1, price: 240 },
      { itemId: "m7", name: "Garden Green Smoothie", qty: 2, price: 180 },
    ],
    total: 600,
    status: "ready",
    placedAt: new Date(Date.now() - 14 * 60_000).toISOString(),
  },
  {
    id: "ORD-2044",
    table: 2,
    items: [
      { itemId: "m2", name: "Avocado Garden Bowl", qty: 1, price: 280 },
    ],
    total: 280,
    status: "served",
    placedAt: new Date(Date.now() - 28 * 60_000).toISOString(),
  },
];

export function formatINR(n: number) {
  return n.toLocaleString("en-IN", {
    style: "currency",
    currency: "INR",
    minimumFractionDigits: 2,
    maximumFractionDigits: 2
  });
}

export function relativeTime(iso: string) {
  const diff = Math.max(1, Math.round((Date.now() - new Date(iso).getTime()) / 60_000));
  return `${diff} min ago`;
}