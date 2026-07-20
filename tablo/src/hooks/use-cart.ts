import { useState } from "react";
import { MenuItem } from "@/types";

export type CartLine = { item: MenuItem; qty: number };

export function useCart() {
  const [cart, setCart] = useState<CartLine[]>([]);

  const cartCount = cart.reduce((s, l) => s + l.qty, 0);
  const cartTotal = cart.reduce((s, l) => s + l.qty * l.item.price, 0);

  const updateQty = (item: MenuItem, delta: number) => {
    setCart((prev) => {
      const found = prev.find((l) => l.item.id === item.id);
      if (!found) return delta > 0 ? [...prev, { item, qty: 1 }] : prev;
      const next = prev.map((l) =>
        l.item.id === item.id ? { ...l, qty: l.qty + delta } : l
      );
      return next.filter((l) => l.qty > 0);
    });
  };

  const qtyOf = (id: string) => cart.find((l) => l.item.id === id)?.qty ?? 0;

  const clearCart = () => setCart([]);

  return {
    cart,
    cartCount,
    cartTotal,
    updateQty,
    qtyOf,
    clearCart,
  };
}
