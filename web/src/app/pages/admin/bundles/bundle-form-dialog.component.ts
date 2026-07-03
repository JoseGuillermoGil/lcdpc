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
import { Bundle, CreateBundleRequest, BundleItemRequest, BundlePrice } from '../../../core/models/bundle.model';
import { Product } from '../../../core/models/product.model';
import { BundleApiService } from '../../../core/services/bundle-api.service';
import { ProductApiService } from '../../../core/services/product-api.service';
import { BranchApiService } from '../../../core/services/branch-api.service';
import { PriceCategoryApiService } from '../../../core/services/price-category-api.service';
import { CategoryStore } from '../../../core/stores/category.store';
import { forkJoin } from 'rxjs';

interface BundleItemForm extends BundleItemRequest {
  priceOptions: { label: string; id: string; amount: number }[];
  selectedPriceId: string | null;
  unitPrice: number;
}

interface PriceOption {
  label: string;
  id: string;
  amount: number;
}

@Component({
  selector: 'app-bundle-form-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, SelectModule, FloatLabelModule
  ],
  templateUrl: './bundle-form-dialog.component.html',
  styleUrl: './bundle-form-dialog.component.scss'
})
export class BundleFormDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() bundle: Bundle | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  private readonly authStore = inject(AuthStore);
  private readonly bundleApi = inject(BundleApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly branchApi = inject(BranchApiService);
  private readonly priceCategoryApi = inject(PriceCategoryApiService);
  readonly categoryStore = inject(CategoryStore);

  protected readonly saving = signal(false);
  protected readonly canViewAllBranches = computed(() => this.authStore.hasPermission('view:branch:all'));
  protected readonly userBranchId = computed(() => this.authStore.currentUser()?.branchId ?? null);
  protected readonly products = signal<Product[]>([]);
  protected readonly branches = signal<{ label: string; value: string }[]>([]);
  protected readonly priceCategories = signal<{ id: string; name: string }[]>([]);
  protected readonly bundlePrices = signal<BundlePrice[]>([]);
  protected readonly loadingPrices = signal(false);
  protected submitted = false;
  protected imageFile: File | null = null;
  protected imagePreview: string | null = null;

  protected form = this.emptyForm();

  protected get isEditMode(): boolean {
    return this.bundle !== null;
  }

  protected get availableProducts() {
    return this.products().filter((p) => p.isActive);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.loadProducts();
      this.loadBranches();
      this.loadPriceCategories();
      if (this.bundle) {
        this.form = {
          code: this.bundle.code,
          name: this.bundle.name,
          category_id: this.bundle.categoryId,
          branch_id: this.bundle.branchId,
          items: (this.bundle.items ?? []).map((i) => ({
            product_id: i.productId,
            quantity: i.quantity,
            priceOptions: [],
            selectedPriceId: null,
            unitPrice: 0,
          })),
        };
        this.imagePreview = this.bundleApi.resolveImageUrl(this.bundle.img);
        this.loadBundlePrices();
      } else {
        this.form = this.emptyForm();
        if (!this.canViewAllBranches() && this.userBranchId()) {
          this.form.branch_id = this.userBranchId();
        }
        this.imagePreview = null;
        this.bundlePrices.set([]);
      }
      this.submitted = false;
      this.imageFile = null;
    }
  }

  private loadBranches(): void {
    this.branchApi.listAdmin().subscribe({
      next: (branches) => this.branches.set(branches.map((b) => ({ label: b.storeName, value: b.id }))),
    });
  }

  private loadProducts(): void {
    this.productApi.list({ limit: 100 }).subscribe({
      next: (res) => this.products.set(res.items),
    });
  }

  private loadPriceCategories(): void {
    this.priceCategoryApi.list().subscribe({
      next: (categories) => this.priceCategories.set(categories.map((c) => ({ id: c.id, name: c.name }))),
    });
  }

  private loadBundlePrices(): void {
    if (!this.bundle) return;
    this.loadingPrices.set(true);
    this.bundleApi.listPrices(this.bundle.bundleId).subscribe({
      next: (prices) => {
        this.bundlePrices.set(prices);
        this.loadingPrices.set(false);
      },
      error: () => this.loadingPrices.set(false),
    });
  }

  addItem(): void {
    this.form.items.push({
      product_id: '',
      quantity: 1,
      priceOptions: [],
      selectedPriceId: null,
      unitPrice: 0,
    });
  }

  removeItem(index: number): void {
    this.form.items.splice(index, 1);
  }

  onProductSelect(index: number): void {
    const item = this.form.items[index];
    item.unitPrice = 0;
    item.priceOptions = [];
    item.selectedPriceId = null;

    const productId = item.product_id;
    if (!productId) return;

    const prices = this.bundlePrices().filter((p) => p.bundleId === this.bundle?.bundleId);
    const options = this.formatPriceOptions(prices);
    item.priceOptions = options;

    if (options.length > 0) {
      item.unitPrice = options[0].amount;
      item.selectedPriceId = options[0].id;
    }
  }

  onPriceOptionSelect(index: number, id: string): void {
    const item = this.form.items[index];
    const option = item.priceOptions.find((o) => o.id === id);
    if (option) {
      item.selectedPriceId = id;
      item.unitPrice = option.amount;
    }
  }

  private formatPriceOptions(prices: BundlePrice[]): PriceOption[] {
    return prices.map((p) => {
      const cat = this.priceCategories().find((c) => c.id === p.priceCategoryId);
      const name = cat?.name ?? 'Precio base';
      return { label: `${name} — $${p.amount.toFixed(2)}`, id: p.id, amount: p.amount };
    });
  }

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.imageFile = input.files[0];
      const reader = new FileReader();
      reader.onload = () => {
        this.imagePreview = reader.result as string;
      };
      reader.readAsDataURL(this.imageFile);
    }
  }

  removeImage(): void {
    this.imageFile = null;
    this.imagePreview = null;
  }

  getItemSubtotal(index: number): number {
    const item = this.form.items[index];
    return item.quantity * item.unitPrice;
  }

  get total(): number {
    return this.form.items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
  }

  get totalItems(): number {
    return this.form.items.length;
  }

  save(): void {
    this.submitted = true;
    if (!this.form.name || !this.form.code || this.form.items.length === 0) {
      return;
    }
    if (this.form.items.some((i) => !i.product_id || i.quantity <= 0)) {
      return;
    }

    this.saving.set(true);

    const req: CreateBundleRequest = {
      code: this.form.code,
      name: this.form.name,
      items: this.form.items.map((i) => ({
        product_id: i.product_id,
        quantity: i.quantity,
      })),
      category_id: this.form.category_id ?? undefined,
      branch_id: this.form.branch_id ?? undefined,
    };

    const operation = this.isEditMode
      ? this.bundleApi.update(this.bundle!.bundleId, req, this.imageFile ?? undefined)
      : this.bundleApi.create(req, this.imageFile ?? undefined);

    operation.subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  close(): void {
    this.closed.emit();
  }

  private emptyForm() {
    return {
      code: '',
      name: '',
      category_id: null as string | null,
      branch_id: null as string | null,
      items: [] as BundleItemForm[],
    };
  }
}
