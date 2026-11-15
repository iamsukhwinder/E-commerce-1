import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiClient } from '../../../../core/api/api-client.service';
import { StorefrontHeader } from '../../../storefront/components/storefront-header/storefront-header';
import { Footer } from '../../../storefront/components/footer/footer';

@Component({
  selector: 'app-orders-page', standalone: true, imports: [CommonModule, RouterLink, StorefrontHeader, Footer],
  template: `<app-storefront-header /><main class="container py-5" style="min-height:45vh"><nav class="mb-4"><a routerLink="/">Home</a><span class="mx-2">/</span>Orders</nav><h1 class="h3 fw-bold mb-4">Your Orders</h1><p *ngIf="loading">Loading orders…</p><section *ngIf="!loading && !orders.length" class="border rounded-4 p-5 text-center"><p class="text-secondary">You have not placed an order yet.</p><a routerLink="/products" class="btn btn-primary rounded-pill">Continue shopping</a></section><article *ngFor="let order of orders" class="border rounded-4 p-4 mb-3"><div class="d-flex justify-content-between"><div><strong>{{order.orderNumber}}</strong><div class="text-secondary small">{{order.createdAt | date:'medium'}}</div></div><div class="text-end"><span class="badge text-bg-success">{{order.status}}</span><div class="fw-bold mt-2">{{order.total | currency:'USD'}}</div></div></div><div class="mt-3 text-secondary">{{order.items.length}} item(s) · {{order.paymentMethod}}</div></article></main><app-footer />`,
})
export class OrdersPage implements OnInit {
  private readonly api = inject(ApiClient); orders: any[] = []; loading = true;
  ngOnInit(): void { this.api.get<any[]>('/orders').subscribe({ next: rows => { this.orders = rows; this.loading = false; }, error: () => this.loading = false }); }
}
