import { Injectable, signal, computed, inject } from '@angular/core';
import { ApiClient } from '../api/api-client.service';

export interface WishlistItem {
  id: string;
  name: string;
  brand: string;
  image: string;
  price: number;
  originalPrice?: number;
  rating?: number;
  reviewCount?: number;
  variant?: string;
  inStock?: boolean;
  categoryTag?: string;
  addedAt?: number;
}

export interface WishlistToast {
  id: number;
  message: string;
  productName: string;
  image?: string;
  added: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class WishlistService {
  private readonly api = inject(ApiClient);

  // State signals
  private readonly _items = signal<WishlistItem[]>([]);
  private readonly _toast = signal<WishlistToast | null>(null);
  private toastTimeoutId: any = null;

  // Read-only public signals
  readonly items = this._items.asReadonly();
  readonly toast = this._toast.asReadonly();

  // Computed metrics
  readonly count = computed(() => this._items().length);
  readonly isEmpty = computed(() => this._items().length === 0);

  // Set of wishlisted item IDs for fast O(1) reactive checks
  readonly itemIdsSet = computed(() => new Set(this._items().map((item) => item.id)));

  constructor() { this.refresh(); }
  refresh(): void { if (typeof window === 'undefined' || !localStorage.getItem('yourstore_auth_token')) return; this.api.get<any[]>('/wishlist').subscribe({ next: rows => this._items.set(rows.map(x => ({ id: String(x.productId), name: x.product.name, brand: x.product.brand, image: x.product.imageUrl, price: x.product.price, originalPrice: x.product.originalPrice, inStock: x.product.inStock, addedAt: new Date(x.createdAt).getTime() }))), error: () => this._items.set([]) }); }

  /**
   * Check if a product ID is in the wishlist
   */
  isInWishlist(productId: string): boolean {
    if (!productId) return false;
    return this.itemIdsSet().has(productId);
  }

  /**
   * Toggle product in wishlist:
   * Adds if not present, removes if already present.
   * Returns true if added, false if removed.
   */
  toggleWishlist(product: Partial<WishlistItem> & { id: string; name: string }): boolean {
    if (!product || !product.id) return false;

    const exists = this.isInWishlist(product.id);

    if (exists) {
      this.removeFromWishlist(product.id);
      this.showToast({
        id: Date.now(),
        message: 'Removed from your wishlist',
        productName: product.name,
        image: product.image,
        added: false,
      });
      return false;
    } else {
      const newItem: WishlistItem = {
        id: product.id,
        name: product.name,
        brand: product.brand || 'Your Store Authentic',
        image: product.image || '/images/wellness/neem-turmeric.jpg',
        price: product.price ?? 0,
        originalPrice: product.originalPrice,
        rating: product.rating ?? 5.0,
        reviewCount: product.reviewCount ?? 120,
        variant: product.variant || 'Standard',
        inStock: product.inStock ?? true,
        categoryTag: product.categoryTag || 'Wellness',
        addedAt: Date.now(),
      };

      const updated = [newItem, ...this._items()];
      this._items.set(updated);
      this.api.post('/wishlist', { productId: Number(product.id) }).subscribe({ error: () => this.refresh() });

      this.showToast({
        id: Date.now(),
        message: 'Added to your wishlist!',
        productName: product.name,
        image: product.image,
        added: true,
      });

      return true;
    }
  }

  /**
   * Add a product explicitly
   */
  addToWishlist(item: WishlistItem): void {
    if (this.isInWishlist(item.id)) return;
    const updated = [item, ...this._items()];
    this._items.set(updated);
    this.api.post('/wishlist', { productId: Number(item.id) }).subscribe({ error: () => this.refresh() });
  }

  /**
   * Remove a product by id
   */
  removeFromWishlist(productId: string): void {
    const updated = this._items().filter((item) => item.id !== productId);
    this._items.set(updated);
    this.api.delete(`/wishlist/${productId}`).subscribe({ error: () => this.refresh() });
  }

  /**
   * Clear all items from the wishlist
   */
  clearWishlist(): void {
    const ids = this._items().map(x => x.id); this._items.set([]); ids.forEach(id => this.api.delete(`/wishlist/${id}`).subscribe());
  }

  /**
   * Notification toast
   */
  showToast(toast: WishlistToast): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this._toast.set(toast);
    this.toastTimeoutId = setTimeout(() => {
      this._toast.set(null);
    }, 3500);
  }

  dismissToast(): void {
    if (this.toastTimeoutId) {
      clearTimeout(this.toastTimeoutId);
    }
    this._toast.set(null);
  }

}
