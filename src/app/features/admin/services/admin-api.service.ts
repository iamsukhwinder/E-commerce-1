import { inject, Injectable } from '@angular/core';
import { ApiClient } from '../../../core/api/api-client.service';

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly api = inject(ApiClient);
  dashboard() { return this.api.get<any>('/admin/dashboard'); }
  products() { return this.api.get<any[]>('/admin/products'); }
  createProduct(value: any) { return this.api.post<any>('/admin/products', value); }
  deleteProduct(id: number) { return this.api.delete(`/admin/products/${id}`); }
  categories() { return this.api.get<any[]>('/admin/categories'); }
  createCategory(value: any) { return this.api.post<any>('/admin/categories', value); }
  deleteCategory(id: number) { return this.api.delete(`/admin/categories/${id}`); }
  brands() { return this.api.get<any[]>('/admin/brands'); }
  createBrand(value: any) { return this.api.post<any>('/admin/brands', value); }
  deleteBrand(id: number) { return this.api.delete(`/admin/brands/${id}`); }
  inventory(lowStockOnly = false) { return this.api.get<any[]>('/admin/inventory', { lowStockOnly }); }
  updateInventory(id: number, quantity: number, lowStockThreshold: number) { return this.api.put(`/admin/inventory/${id}`, { quantity, lowStockThreshold }); }
  reports() { return this.api.get<any>('/admin/reports'); }
  settings() { return this.api.get<Record<string, string>>('/admin/settings'); }
  saveSettings(value: Record<string, string>) { return this.api.put<Record<string, string>>('/admin/settings', value); }
}
