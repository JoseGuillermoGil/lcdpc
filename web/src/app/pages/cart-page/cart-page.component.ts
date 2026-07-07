import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { AuthStore } from '../../core/auth/auth.store';
import { CartItem, CartStore } from '../../core/stores/cart.store';
import { BranchStore } from '../../core/stores/branch.store';
import { SystemConfigStore } from '../../core/stores/system-config.store';
import { OrderApiService } from '../../core/services/order-api.service';
import { LoginDialogComponent } from '../../shared/login-dialog/login-dialog.component';

@Component({
  selector: 'app-cart-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    TagModule,
    ToastModule,
    LoginDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './cart-page.component.html',
  styleUrl: './cart-page.component.scss',
})
export class CartPageComponent {
  private readonly cartStore = inject(CartStore);
  private readonly authStore = inject(AuthStore);
  private readonly branchStore = inject(BranchStore);
  protected readonly systemConfigStore = inject(SystemConfigStore);
  private readonly orderApi = inject(OrderApiService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly items = this.cartStore.items;
  protected readonly totalPrice = this.cartStore.totalPrice;
  protected readonly totalQuantity = this.cartStore.totalQuantity;
  protected readonly isAuthenticated = this.authStore.isAuthenticated;
  protected readonly currentUser = this.authStore.currentUser;

  protected readonly buying = signal(false);
  protected readonly showLoginDialog = signal(false);
  protected readonly contactName = signal('');
  protected readonly phone = signal('');
  protected readonly address = signal('');
  protected readonly notes = signal('');

  protected readonly isEmpty = computed(() => this.items().length === 0);
  protected readonly hasStockIssues = computed(() =>
    !this.systemConfigStore.negativeStock() && this.items().some((item) => item.quantity > item.stockAvailable)
  );

  protected readonly branchId = computed(() => {
    const items = this.items();
    if (items.length === 0) return null;
    return items[0].branchId;
  });

  protected readonly summaryLines = computed(() => {
    const lines = [
      this.contactName().trim() ? `Nombre: ${this.contactName().trim()}` : '',
      this.phone().trim() ? `WhatsApp: ${this.phone().trim()}` : '',
      this.address().trim() ? `Dirección: ${this.address().trim()}` : '',
      this.notes().trim() ? `Notas: ${this.notes().trim()}` : '',
    ].filter(Boolean);

    return lines.join('\n');
  });

  protected onImageError(event: Event): void {
    (event.target as HTMLImageElement).src = '/not-found.png';
  }

  protected clearCart(): void {
    this.cartStore.clear();
  }

  protected removeItem(id: string): void {
    this.cartStore.removeItem(id);
  }

  protected increment(id: string): void {
    const item = this.items().find((i) => i.id === id);
    if (item && (this.systemConfigStore.negativeStock() || item.quantity < item.stockAvailable)) {
      this.cartStore.increment(id);
    }
  }

  protected decrement(id: string): void {
    this.cartStore.decrement(id);
  }

  isOverStock(item: CartItem): boolean {
    return !this.systemConfigStore.negativeStock() && item.quantity > item.stockAvailable;
  }

  protected openLogin(): void {
    this.showLoginDialog.set(true);
  }

  protected onLoginSuccess(): void {
    this.showLoginDialog.set(false);
    this.buy();
  }

  protected buy(): void {
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
    const branchId = this.branchId();
    if (!user || !branchId) return;

    this.buying.set(true);

    this.orderApi.create({
      branch_id: branchId,
      client_user_id: user.id,
      notes: this.summaryLines(),
      items: this.items().map((item) => ({
        item_type: item.itemType,
        ...(item.itemType === 'bundle' ? { bundle_id: item.id } : { product_id: item.id }),
        quantity: item.quantity,
        unit_price: item.price,
      })),
    }).subscribe({
      next: () => {
        this.cartStore.clear();
        this.cartStore.notifyOrderCreated();
        this.buying.set(false);
        this.messageService.add({
          severity: 'success',
          summary: 'Éxito',
          detail: 'Orden creada exitosamente',
        });
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

  protected goBack(): Promise<boolean> {
    return this.router.navigateByUrl('/');
  }
}
