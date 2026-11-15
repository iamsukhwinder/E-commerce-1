import { Injectable, signal, computed, inject } from '@angular/core';
import { ApiClient } from '../api/api-client.service';

export interface CartItem {
  id: string; // Composite key: `${productId}_${variant}`
  productId: string;
  name: string;
  brand: string;
  image: string;
  price: number;
  originalPrice?: number;
  discountPercent?: number;
  variant: string;
  quantity: number;
}

export interface CartToast {
  id: number;
  message: string;
  productName: string;
  variant?: string;
  image?: string;
  price?: number;
}

@Injectable({
  providedIn: 'root',
})
export class CartService {
  private readonly api = inject(ApiClient);

  // State signals
  private readonly _items = signal<CartItem[]>([]);
  private readonly _toast = signal<CartToast | null>(null);
  private toastTimeoutId: any = null;

  // Read-only public signals
  readonly items = this._items.asReadonly();
  readonly toast = this._toast.asReadonly();

  // Computed metrics
  readonly totalQuantity = computed(() =>
    this._items().reduce((total, item) => total + item.quantity, 0)
  );

  readonly subtotal = computed(() =>
    this._items().reduce((sum, item) => sum + item.price * item.quantity, 0)
  );

  readonly shipping = computed(() => (this.subtotal() > 50 || this.subtotal() === 0 ? 0 : 4.99));

  readonly estimatedTax = computed(() => +(this.subtotal() * 0.05).toFixed(2));

  readonly total = computed(() => +(this.subtotal() + this.shipping() + this.estimatedTax()).toFixed(2));

  readonly isEmpty = computed(() => this._items().length === 0);

  constructor() { this.refresh(); }

  refresh(): void {
    if (typeof window === 'undefined' || !localStorage.getItem('yourstore_auth_token')) { this._items.set([]); return; }
    this.api.get<any[]>('/cart').subscribe({ next: rows => this._items.set(rows.map(row => this.fromApi(row))), error: () => this._items.set([]) });
  }

  /**
   * Add a product to cart.
   * If the item with the same productId and variant exists, increase its quantity.
   * Otherwise add a new item entry.
   */
  addToCart(item: Omit<CartItem, 'id' | 'quantity'> & { quantity?: number }, qty: number = 1): void {
    const quantityToAdd = Math.max(1, qty ?? item.quantity ?? 1);
    const variantKey = (item.variant || 'Standard').trim();
    const compositeId = `${item.productId}_${variantKey.toLowerCase().replace(/\s+/g, '-')}`;

    const currentItems = this._items();
    const existingIndex = currentItems.findIndex((i) => i.id === compositeId);

    let updatedItems: CartItem[];

    if (existingIndex > -1) {
      // Update quantity
      updatedItems = currentItems.map((cartItem, index) => {
        if (index === existingIndex) {
          return {
            ...cartItem,
            quantity: cartItem.quantity + quantityToAdd,
          };
        }
        return cartItem;
      });
    } else {
      // Add new cart entry
      const newItem: CartItem = {
        ...item,
        id: compositeId,
        variant: variantKey,
        quantity: quantityToAdd,
      };
      updatedItems = [...currentItems, newItem];
    }

    this._items.set(updatedItems);
    const target = updatedItems.find(i => i.id === compositeId)!;
    this.api.put<any[]>('/cart/items', { productId: Number(item.productId), quantity: target.quantity, variant: variantKey })
      .subscribe({ next: rows => this._items.set(rows.map(row => this.fromApi(row))), error: () => this.refresh() });

    // Trigger toast notification
    this.showToast({
      id: Date.now(),
      message: 'Added to your shopping cart!',
      productName: item.name,
      variant: variantKey,
      image: item.image,
      price: item.price,
    });
  }

  /**
   * Update item quantity directly
   */
  updateQuantity(itemId: string, newQuantity: number): void {
    if (newQuantity <= 0) {
      this.removeItem(itemId);
      return;
    }

    const updated = this._items().map((item) =>
      item.id === itemId ? { ...item, quantity: newQuantity } : item
    );

    this._items.set(updated);
    const item = updated.find(i => i.id === itemId);
    if (item) this.api.put<any[]>('/cart/items', { productId: Number(item.productId), quantity: item.quantity, variant: item.variant })
      .subscribe({ next: rows => this._items.set(rows.map(row => this.fromApi(row))), error: () => this.refresh() });
  }

  /**
   * Increment quantity
   */
  incrementQuantity(itemId: string): void {
    const item = this._items().find((i) => i.id === itemId);
    if (item) {
      this.updateQuantity(itemId, item.quantity + 1);
    }
  }

  /**
   * Decrement quantity
   */
  decrementQuantity(itemId: string): void {
    const item = this._items().find((i) => i.id === itemId);
    if (item) {
      this.updateQuantity(itemId, item.quantity - 1);
    }
  }

  /**
   * Remove item from cart
   */
  removeItem(itemId: string): void {
    const updated = this._items().filter((i) => i.id !== itemId);
    const removed = this._items().find(i => i.id === itemId);
    this._items.set(updated);
    if (removed) this.api.delete(`/cart/items/${removed.id}`).subscribe({ error: () => this.refresh() });
  }

  /**
   * Clear all cart items
   */
  clearCart(): void {
    this._items.set([]);
    this.api.delete('/cart').subscribe({ error: () => this.refresh() });
  }

  /**
   * Show toast confirmation notification
   */
  showToast(toast: CartToast): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this._toast.set(toast);
    this.toastTimeoutId = setTimeout(() => {
      this._toast.set(null);
    }, 4000);
  }

  /**
   * Dismiss toast immediately
   */
  dismissToast(): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this._toast.set(null);
  }

  private fromApi(row: any): CartItem { return { id: String(row.id), productId: String(row.productId), name: row.product.name, brand: row.product.brand, image: row.product.imageUrl || '', price: row.product.price, originalPrice: row.product.originalPrice, variant: row.variant || 'Standard', quantity: row.quantity }; }
}
