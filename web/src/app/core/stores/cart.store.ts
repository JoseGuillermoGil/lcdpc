import { Injectable, computed, signal } from '@angular/core';

const CART_KEY = 'lcdpc_cart';

export interface CartItem {
  id: string;
  name: string;
  imageUrl: string;
  price: number;
  branchId: string | null;
  quantity: number;
}

function loadCart(): CartItem[] {
  try {
    const raw = localStorage.getItem(CART_KEY);
    if (!raw) return [];
    return JSON.parse(raw) as CartItem[];
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

  readonly items = this._items.asReadonly();

  readonly totalItems = computed(() => this._items().length);

  readonly totalPrice = computed(() =>
    this._items().reduce((sum, item) => sum + item.price * item.quantity, 0)
  );

  addItem(item: Omit<CartItem, 'quantity'>, quantity: number = 1): void {
    this._items.update((items) => {
      if (items.length > 0 && item.branchId !== null) {
        const currentBranchId = items[0].branchId ?? null;
        if (currentBranchId !== null && currentBranchId !== item.branchId) {
          const next = [{ ...item, quantity } as CartItem];
          saveCart(next);
          return next;
        }
      }
      const existing = items.find((i) => i.id === item.id);
      let next: CartItem[];
      if (existing) {
        next = items.map((i) =>
          i.id === item.id ? { ...i, quantity: i.quantity + quantity } : i
        );
      } else {
        next = [...items, { ...item, quantity }];
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
      const next = items.map((i) => (i.id === id ? { ...i, quantity } : i));
      saveCart(next);
      return next;
    });
  }

  increment(id: string): void {
    this._items.update((items) => {
      const next = items.map((i) =>
        i.id === id ? { ...i, quantity: i.quantity + 1 } : i
      );
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
}
