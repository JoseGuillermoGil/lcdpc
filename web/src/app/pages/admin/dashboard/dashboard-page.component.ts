import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ProductApiService } from '../../../core/services/product-api.service';
import { BundleApiService } from '../../../core/services/bundle-api.service';
import { OrderApiService } from '../../../core/services/order-api.service';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent implements OnInit {
  private readonly productApi = inject(ProductApiService);
  private readonly bundleApi = inject(BundleApiService);
  private readonly orderApi = inject(OrderApiService);

  protected readonly loading = signal(true);
  protected readonly stats = signal({
    totalProducts: 0,
    totalBundles: 0,
    totalOrders: 0,
    pendingOrders: 0,
  });

  ngOnInit(): void {
    forkJoin({
      products: this.productApi.list({ limit: 1 }),
      bundles: this.bundleApi.list({ limit: 1 }),
      orders: this.orderApi.list({ limit: 100 }),
      pendingOrders: this.orderApi.list({ limit: 100, status: 'PENDING_REVIEW' }),
    }).subscribe({
      next: ({ products, bundles, orders, pendingOrders }) => {
        this.stats.set({
          totalProducts: products.totalCount,
          totalBundles: bundles.totalCount,
          totalOrders: orders.length,
          pendingOrders: pendingOrders.length,
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }
}
