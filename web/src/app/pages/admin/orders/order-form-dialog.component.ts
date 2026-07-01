import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output, signal, SimpleChanges, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { FloatLabelModule } from 'primeng/floatlabel';
import { AuthStore } from '../../../core/auth/auth.store';
import { AppUser } from '../../../core/models/user.model';
import { UserApiService } from '../../../core/services/user-api.service';
import { OrderApiService } from '../../../core/services/order-api.service';
import { ProductApiService } from '../../../core/services/product-api.service';
import { PriceApiService } from '../../../core/services/price-api.service';
import { Product } from '../../../core/models/product.model';
import { CreateOrderRequest } from '../../../core/models/order.model';

interface OrderItemForm {
  product_id: string;
  quantity: number;
  unit_price: number;
}

@Component({
  selector: 'app-order-form-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, SelectModule, FloatLabelModule
  ],
  templateUrl: './order-form-dialog.component.html',
  styleUrl: './order-form-dialog.component.scss'
})
export class OrderFormDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() branches: { id: string; name: string }[] = [];

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();

  private readonly authStore = inject(AuthStore);
  private readonly userApi = inject(UserApiService);
  private readonly orderApi = inject(OrderApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly priceApi = inject(PriceApiService);

  protected readonly saving = signal(false);
  protected readonly canViewAllBranches = computed(() => this.authStore.hasPermission('view:branch:all'));
  protected readonly userBranchId = computed(() => this.authStore.currentUser()?.branchId ?? null);
  protected readonly products = signal<Product[]>([]);
  protected submitted = false;

  protected form = this.emptyForm();

  protected documentQuery = '';
  protected selectedUser = signal<AppUser | null>(null);
  protected userSearchError = signal('');
  protected searchingUser = signal(false);

  protected get total(): number {
    return this.form.items.reduce((sum, item) => sum + (item.quantity * item.unit_price), 0);
  }

  protected get totalItems(): number {
    return this.form.items.reduce((sum, item) => sum + item.quantity, 0);
  }

  protected get duplicateProductIds(): Set<string> {
    const seen = new Set<string>();
    const dupes = new Set<string>();
    for (const item of this.form.items) {
      if (item.product_id && seen.has(item.product_id)) {
        dupes.add(item.product_id);
      }
      seen.add(item.product_id);
    }
    return dupes;
  }

  protected get hasDuplicates(): boolean {
    return this.duplicateProductIds.size > 0;
  }

  protected get hasStockIssues(): boolean {
    return this.form.items.some((item) => {
      if (!item.product_id) return false;
      const product = this.products().find((p) => p.productId === item.product_id);
      if (!product) return false;
      return product.stockBlocked + item.quantity > product.stock;
    });
  }

  protected isDuplicateItem(index: number): boolean {
    const item = this.form.items[index];
    return !!item.product_id && this.duplicateProductIds.has(item.product_id);
  }

  protected isOverStock(index: number): boolean {
    const item = this.form.items[index];
    if (!item.product_id) return false;
    const product = this.products().find((p) => p.productId === item.product_id);
    if (!product) return false;
    return product.stockBlocked + item.quantity > product.stock;
  }

  protected getItemStockInfo(index: number): string {
    const item = this.form.items[index];
    if (!item.product_id) return '';
    const product = this.products().find((p) => p.productId === item.product_id);
    if (!product) return '';
    const available = product.stock - product.stockBlocked;
    return `Disponible: ${available} | Bloqueado: ${product.stockBlocked} | Total: ${product.stock}`;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.form = this.emptyForm();
      this.selectedUser.set(null);
      this.documentQuery = '';
      this.userSearchError.set('');
      this.searchingUser.set(false);
      this.products.set([]);
      this.submitted = false;

      if (!this.canViewAllBranches() && this.userBranchId()) {
        this.form.branch_id = this.userBranchId()!;
        this.loadProducts(this.form.branch_id);
      }
    }
  }

  protected onBranchChange(): void {
    this.form.items = [];
    if (this.form.branch_id) {
      this.loadProducts(this.form.branch_id);
    }
  }

  private loadProducts(branchId: string): void {
    this.productApi.list({ branch_id: branchId, limit: 100 }).subscribe({
      next: (res) => this.products.set(res.items.filter((p) => p.isActive)),
    });
  }

  protected searchByDocument(): void {
    const doc = this.documentQuery.trim();
    if (!doc) return;

    this.searchingUser.set(true);
    this.userSearchError.set('');
    this.selectedUser.set(null);
    this.form.client_user_id = '';

    this.userApi.getByDocument(doc).subscribe({
      next: (user) => {
        this.selectedUser.set(user);
        this.form.client_user_id = user.id;
        this.searchingUser.set(false);
      },
      error: () => {
        this.userSearchError.set('No se encontró un usuario con ese documento');
        this.searchingUser.set(false);
      },
    });
  }

  protected clearUser(): void {
    this.selectedUser.set(null);
    this.form.client_user_id = '';
    this.documentQuery = '';
    this.userSearchError.set('');
  }

  protected formatUserName(user: AppUser): string {
    return [user.firstName, user.lastName].filter((v) => !!v).join(' ') || user.email;
  }

  protected onProductSelect(index: number): void {
    const item = this.form.items[index];
    if (!item.product_id) return;
    this.priceApi.listByProductId(item.product_id).subscribe({
      next: (prices) => {
        if (prices.length > 0 && item.unit_price === 0) {
          item.unit_price = prices[0].amount;
        }
      },
    });
  }

  protected addItem(): void {
    this.form.items.push({ product_id: '', quantity: 1, unit_price: 0 });
  }

  protected removeItem(index: number): void {
    this.form.items.splice(index, 1);
  }

  protected getProductName(productId: string): string {
    return this.products().find((p) => p.productId === productId)?.name ?? '';
  }

  protected save(): void {
    this.submitted = true;
    if (!this.form.branch_id || !this.form.client_user_id) return;
    if (this.form.items.length === 0) return;
    if (this.form.items.some((i) => !i.product_id || i.quantity <= 0)) return;
    if (this.hasDuplicates) return;
    if (this.hasStockIssues) return;

    this.saving.set(true);

    const req: CreateOrderRequest = {
      branch_id: this.form.branch_id,
      client_user_id: this.form.client_user_id,
      notes: this.form.notes,
      items: this.form.items.map((i) => ({
        item_type: 'product',
        product_id: i.product_id,
        quantity: i.quantity,
        unit_price: i.unit_price,
      })),
    };

    this.orderApi.create(req).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  protected close(): void {
    this.visibleChange.emit(false);
  }

  private emptyForm() {
    return {
      branch_id: '',
      client_user_id: '',
      notes: '',
      items: [] as OrderItemForm[],
    };
  }
}
