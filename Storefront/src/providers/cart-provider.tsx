'use client';

import { createContext, useCallback, useEffect, useState } from 'react';

export interface CartItem {
  productId: string;
  productName: string;
  slug: string;
  price: number;
  currency: string;
  quantity: number;
  imageUrl: string | null;
  material: string | null;
}

export interface CartTotals {
  subtotal: number;
  itemCount: number;
  currency: string;
}

export interface CartContextValue {
  items: CartItem[];
  totals: CartTotals;
  addItem: (item: Omit<CartItem, 'quantity'>, quantity?: number) => void;
  removeItem: (productId: string) => void;
  updateQuantity: (productId: string, quantity: number) => void;
  clearCart: () => void;
}

const CART_STORAGE_KEY = 'storefront-cart';

export const CartContext = createContext<CartContextValue | null>(null);

function computeTotals(items: CartItem[]): CartTotals {
  return {
    subtotal: items.reduce((sum, item) => sum + item.price * item.quantity, 0),
    itemCount: items.reduce((count, item) => count + item.quantity, 0),
    currency: items[0]?.currency || 'EUR',
  };
}

function loadCart(): CartItem[] {
  if (typeof window === 'undefined') return [];
  try {
    const stored = localStorage.getItem(CART_STORAGE_KEY);
    if (stored) {
      const parsed = JSON.parse(stored);
      return parsed.items || [];
    }
  } catch {}
  return [];
}

function saveCart(items: CartItem[]) {
  if (typeof window === 'undefined') return;
  localStorage.setItem(
    CART_STORAGE_KEY,
    JSON.stringify({ items, updatedAt: new Date().toISOString() })
  );
}

export function CartProvider({ children }: { children: React.ReactNode }) {
  const [items, setItems] = useState<CartItem[]>([]);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setItems(loadCart());
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted) saveCart(items);
  }, [items, mounted]);

  const addItem = useCallback(
    (item: Omit<CartItem, 'quantity'>, quantity = 1) => {
      setItems((prev) => {
        const existing = prev.find((i) => i.productId === item.productId);
        if (existing) {
          return prev.map((i) =>
            i.productId === item.productId
              ? { ...i, quantity: i.quantity + quantity }
              : i
          );
        }
        return [...prev, { ...item, quantity }];
      });
    },
    []
  );

  const removeItem = useCallback((productId: string) => {
    setItems((prev) => prev.filter((i) => i.productId !== productId));
  }, []);

  const updateQuantity = useCallback((productId: string, quantity: number) => {
    if (quantity < 1) return;
    setItems((prev) =>
      prev.map((i) => (i.productId === productId ? { ...i, quantity } : i))
    );
  }, []);

  const clearCart = useCallback(() => {
    setItems([]);
  }, []);

  const totals = computeTotals(items);

  return (
    <CartContext.Provider
      value={{ items, totals, addItem, removeItem, updateQuantity, clearCart }}
    >
      {children}
    </CartContext.Provider>
  );
}
