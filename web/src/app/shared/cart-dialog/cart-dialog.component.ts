import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject, computed, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { CartStore, CartItem } from '../../core/stores/cart.store';
import { AuthStore } from '../../core/auth/auth.store';
import { BranchStore } from '../../core/stores/branch.store';
import { SystemConfigStore } from '../../core/stores/system-config.store';
import { OrderApiService } from '../../core/services/order-api.service';
import { ProductApiService } from '../../core/services/product-api.service';
import { LoginDialogComponent } from '../login-dialog/login-dialog.component';

@Component({
  selector: 'app-cart-dialog',
  standalone: true,
  imports: [CommonModule, ButtonModule, DialogModule, TagModule, ToastModule, LoginDialogComponent],
  providers: [MessageService],
  templateUrl: './cart-dialog.component.html',
  styleUrl: './cart-dialog.component.scss',
})
export class CartDialogComponent {
  private readonly cartStore = inject(CartStore);
  private readonly authStore = inject(AuthStore);
  private readonly branchStore = inject(BranchStore);
  protected readonly systemConfigStore = inject(SystemConfigStore);
  private readonly orderApi = inject(OrderApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly messageService = inject(MessageService);

  @Input() visible = false;
  @Output() visibleChange = new EventEmitter<boolean>();

  protected readonly buying = signal(false);
  protected readonly showLoginDialog = signal(false);

  protected readonly items = this.cartStore.items;
  protected readonly totalPrice = this.cartStore.totalPrice;
  protected readonly totalQuantity = this.cartStore.totalQuantity;
  protected readonly isAuthenticated = this.authStore.isAuthenticated;
  protected readonly currentUser = this.authStore.currentUser;

  protected readonly isEmpty = computed(() => this.items().length === 0);

  protected readonly hasStockIssues = computed(() =>
    !this.systemConfigStore.negativeStock() && this.items().some((item) => item.quantity > item.stockAvailable)
  );

  protected readonly branchId = computed(() => {
    const items = this.items();
    if (items.length === 0) return null;
    return items[0].branchId;
  });

  close(): void {
    this.visibleChange.emit(false);
  }

  onImageError(event: Event): void {
    (event.target as HTMLImageElement).src = '/not-found.png';
  }

  clearCart(): void {
    this.cartStore.clear();
  }

  removeItem(id: string): void {
    this.cartStore.removeItem(id);
  }

  increment(id: string): void {
    const item = this.items().find((i) => i.id === id);
    if (item && (this.systemConfigStore.negativeStock() || item.quantity < item.stockAvailable)) {
      this.cartStore.increment(id);
    }
  }

  decrement(id: string): void {
    this.cartStore.decrement(id);
  }

  isOverStock(item: CartItem): boolean {
    return !this.systemConfigStore.negativeStock() && item.quantity > item.stockAvailable;
  }

  openLogin(): void {
    this.showLoginDialog.set(true);
  }

  onLoginSuccess(): void {
    this.showLoginDialog.set(false);
    this.buy();
  }

  buy(): void {
    if (this.isEmpty()) return;

    if (!this.isAuthenticated()) {
      return;
    }

    if (this.hasStockIssues()) {
      this.messageService.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Hay productos con cantidad mayor al stock disponible',
      });
      return;
    }

    const user = this.currentUser();
    const bId = this.branchId();
    if (!user || !bId) return;

    this.buying.set(true);

    const req = {
      branch_id: bId,
      client_user_id: user.id,
      notes: '',
      items: this.items().map((item) => ({
        item_type: item.itemType,
        ...(item.itemType === 'bundle' ? { bundle_id: item.id } : { product_id: item.id }),
        quantity: item.quantity,
        unit_price: item.price,
      })),
    };

    this.orderApi.create(req).subscribe({
      next: () => {
        this.cartStore.clear();
        this.cartStore.notifyOrderCreated();
        this.buying.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Orden creada exitosamente',
        });
        this.close();
      },
      error: (err) => {
        this.buying.set(false);
        const msg = err?.error?.message || 'Error al crear la orden';
        this.messageService.add({
          severity: 'error',
          summary: 'Error',
          detail: msg,
        });
      },
    });
  }
}
