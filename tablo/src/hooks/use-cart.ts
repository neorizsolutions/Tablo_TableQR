import { useState } from "react";
import { MenuItem } from "@/types";

export interface SelectedModifier {
  modifierGroupId: string;
  modifierOptionId: string;
  optionName: string;
  priceDelta: number;
}

export interface CartLine {
  lineId: string;
  item: MenuItem;
  qty: number;
  selectedModifiers?: SelectedModifier[];
  specialInstructions?: string;
  releaseState?: "Fired" | "Held";
  courseName?: string;
}

export function computeLineId(
  itemId: string,
  selectedModifiers?: SelectedModifier[],
  specialInstructions?: string,
  releaseState?: string,
  courseName?: string
): string {
  const mods = (selectedModifiers || [])
    .slice()
    .sort((a, b) => a.modifierOptionId.localeCompare(b.modifierOptionId))
    .map((m) => `${m.modifierGroupId}:${m.modifierOptionId}`)
    .join("|");
  const notes = (specialInstructions || "").trim().toLowerCase();
  const rel = releaseState || "Fired";
  const course = (courseName || "").trim().toLowerCase();
  return `${itemId}__mods:${mods}__notes:${notes}__rel:${rel}__c:${course}`;
}

export function useCart() {
  const [cart, setCart] = useState<CartLine[]>([]);

  const cartCount = cart.reduce((s, l) => s + l.qty, 0);
  const cartTotal = cart.reduce((s, l) => {
    const modTotal = l.selectedModifiers?.reduce((acc, m) => acc + (m.priceDelta || 0), 0) || 0;
    return s + l.qty * (l.item.price + modTotal);
  }, 0);

  const addOrUpdateItem = (
    item: MenuItem,
    qtyDelta: number,
    selectedModifiers?: SelectedModifier[],
    specialInstructions?: string,
    releaseState?: "Fired" | "Held",
    courseName?: string
  ) => {
    const targetLineId = computeLineId(item.id, selectedModifiers, specialInstructions, releaseState, courseName);
    setCart((prev) => {
      const idx = prev.findIndex((l) => l.lineId === targetLineId);
      if (idx === -1) {
        if (qtyDelta <= 0) return prev;
        return [
          ...prev,
          {
            lineId: targetLineId,
            item,
            qty: qtyDelta,
            selectedModifiers,
            specialInstructions: specialInstructions?.trim() || undefined,
            releaseState: releaseState || "Fired",
            courseName: courseName?.trim() || undefined,
          },
        ];
      }
      const newQty = prev[idx].qty + qtyDelta;
      if (newQty <= 0) {
        return prev.filter((_, i) => i !== idx);
      }
      return prev.map((l, i) => (i === idx ? { ...l, qty: newQty } : l));
    });
  };

  const updateLineQty = (lineId: string, delta: number) => {
    setCart((prev) => {
      const target = prev.find((l) => l.lineId === lineId);
      if (!target) return prev;
      const newQty = target.qty + delta;
      if (newQty <= 0) {
        return prev.filter((l) => l.lineId !== lineId);
      }
      return prev.map((l) => (l.lineId === lineId ? { ...l, qty: newQty } : l));
    });
  };

  const removeLine = (lineId: string) => {
    setCart((prev) => prev.filter((l) => l.lineId !== lineId));
  };

  const updateQty = (item: MenuItem, delta: number) => {
    const hasLines = cart.filter((l) => l.item.id === item.id);
    if (hasLines.length <= 1) {
      if (hasLines.length === 1) {
        updateLineQty(hasLines[0].lineId, delta);
      } else {
        addOrUpdateItem(item, delta);
      }
    } else {
      if (delta < 0 && hasLines.length > 0) {
        updateLineQty(hasLines[0].lineId, delta);
      } else {
        addOrUpdateItem(item, delta);
      }
    }
  };

  const qtyOf = (id: string) =>
    cart.filter((l) => l.item.id === id).reduce((sum, l) => sum + l.qty, 0);

  const linesOf = (id: string) => cart.filter((l) => l.item.id === id);

  const clearCart = () => setCart([]);

  return {
    cart,
    cartCount,
    cartTotal,
    addOrUpdateItem,
    updateLineQty,
    removeLine,
    updateQty,
    qtyOf,
    linesOf,
    clearCart,
  };
}
