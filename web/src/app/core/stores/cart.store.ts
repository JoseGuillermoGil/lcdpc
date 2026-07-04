import { Injectable, computed, signal } from '@angular/core';

const CART_KEY = 'lcdpc_cart';

export interface CartItem {
  id: string;
  name: string;
  imageUrl: string;
  price: number;
  branchId: string | null;
  quantity: number;
  stockAvailable: number;
  itemType: 'product' | 'bundle';
  items?: { name: string; quantity: number }[];
}

function loadCart(): CartItem[] {
  try {
    const raw = localStorage.getItem(CART_KEY);
    if (!raw) return [];
    const items = JSON.parse(raw) as CartItem[];
    return items
      .filter((item) => item.stockAvailable != null && item.stockAvailable > 0)
      .map((item) => ({
        ...item,
        itemType: item.itemType ?? 'product',
      }));
  } catch {
    return [];
  }
}

function saveCart(items: CartItem[]): void {
  localStorage.setItem(CART_KEY, JSON.stringify(items));
}

@Injectable({ providedIn: 'root' })
export class CartStore {
  private readonly _items = signal<CartItem[]>(loadCart());
  private readonly _lastOrderCreatedAt = signal(0);

  readonly items = this._items.asReadonly();
  readonly lastOrderCreatedAt = this._lastOrderCreatedAt.asReadonly();

  readonly totalItems = computed(() => this._items().length);

  readonly totalQuantity = computed(() =>
    this._items().reduce((sum, item) => sum + item.quantity, 0)
  );

  readonly totalPrice = computed(() =>
    this._items().reduce((sum, item) => sum + item.price * item.quantity, 0)
  );

  addItem(item: Omit<CartItem, 'quantity'>, quantity: number = 1): void {
    this._items.update((items) => {
      if (items.length > 0 && item.branchId !== null) {
        const currentBranchId = items[0].branchId ?? null;
        if (currentBranchId !== null && currentBranchId !== item.branchId) {
          const capped = item.stockAvailable > 0 ? Math.min(quantity, item.stockAvailable) : 0;
          const next = [...(capped > 0 ? [{ ...item, quantity: capped } as CartItem] : [])];
          saveCart(next);
          return next;
        }
      }
      const existing = items.find((i) => i.id === item.id);
      let next: CartItem[];
      if (existing) {
        const newQty = existing.quantity + quantity;
        const freshStockAvailable = item.stockAvailable;
        const capped = freshStockAvailable > 0 ? Math.min(newQty, freshStockAvailable) : 0;
        if (capped <= 0) {
          next = items.filter((i) => i.id !== item.id);
        } else {
          next = items.map((i) =>
            i.id === item.id ? { ...i, quantity: capped, stockAvailable: freshStockAvailable } : i
          );
        }
      } else {
        const capped = item.stockAvailable > 0 ? Math.min(quantity, item.stockAvailable) : 0;
        if (capped <= 0) return items;
        next = [...items, { ...item, quantity: capped }];
      }
      saveCart(next);
      return next;
    });
  }

  removeItem(id: string): void {
    this._items.update((items) => {
      const next = items.filter((i) => i.id !== id);
      saveCart(next);
      return next;
    });
  }

  updateQuantity(id: string, quantity: number): void {
    if (quantity <= 0) {
      this.removeItem(id);
      return;
    }
    this._items.update((items) => {
      const next = items.map((i) => {
        if (i.id !== id) return i;
        const capped = i.stockAvailable > 0 ? Math.min(quantity, i.stockAvailable) : 0;
        return { ...i, quantity: capped };
      });
      saveCart(next);
      return next;
    });
  }

  increment(id: string): void {
    this._items.update((items) => {
      const next = items.map((i) => {
        if (i.id !== id) return i;
        if (i.stockAvailable <= 0) return i;
        if (i.quantity >= i.stockAvailable) return i;
        return { ...i, quantity: i.quantity + 1 };
      });
      saveCart(next);
      return next;
    });
  }

  decrement(id: string): void {
    this._items.update((items) => {
      const next = items
        .map((i) => {
          if (i.id !== id) return i;
          const newQty = i.quantity - 1;
          return newQty <= 0 ? null : { ...i, quantity: newQty };
        })
        .filter(Boolean) as CartItem[];
      saveCart(next);
      return next;
    });
  }

  clear(): void {
    this._items.set([]);
    localStorage.removeItem(CART_KEY);
  }

  notifyOrderCreated(): void {
    this._lastOrderCreatedAt.update((v) => v + 1);
  }

  syncStock(stockMap: Map<string, number>): void {
    this._items.update((items) => {
      let changed = false;
      const next = items
        .map((item) => {
          const freshStock = stockMap.get(item.id);
          if (freshStock == null) return item;
          if (freshStock === item.stockAvailable) return item;
          changed = true;
          if (freshStock <= 0) return null;
          const capped = Math.min(item.quantity, freshStock);
          return { ...item, stockAvailable: freshStock, quantity: capped };
        })
        .filter(Boolean) as CartItem[];
      if (changed) saveCart(next);
      return changed ? next : items;
    });
  }
}
