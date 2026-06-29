import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { FloatLabelModule } from 'primeng/floatlabel';
import { Bundle, CreateBundleRequest, BundleItemRequest } from '../../../core/models/bundle.model';
import { Product } from '../../../core/models/product.model';
import { BundleApiService } from '../../../core/services/bundle-api.service';
import { ProductApiService } from '../../../core/services/product-api.service';
import { BranchApiService } from '../../../core/services/branch-api.service';
import { CategoryStore } from '../../../core/stores/category.store';

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

  private readonly bundleApi = inject(BundleApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly branchApi = inject(BranchApiService);
  readonly categoryStore = inject(CategoryStore);

  protected readonly saving = signal(false);
  protected readonly products = signal<Product[]>([]);
  protected readonly branches = signal<{ label: string; value: string }[]>([]);
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
      if (this.bundle) {
        this.form = {
          code: this.bundle.code,
          name: this.bundle.name,
          category_id: this.bundle.categoryId,
          branch_id: this.bundle.branchId,
          items: this.bundle.items?.map((i) => ({
            product_id: i.productId,
            quantity: i.quantity,
          })) ?? [],
        };
        this.imagePreview = this.bundleApi.resolveImageUrl(this.bundle.img);
      } else {
        this.form = this.emptyForm();
        this.imagePreview = null;
      }
      this.submitted = false;
      this.imageFile = null;
    }
  }

  private loadBranches(): void {
    this.branchApi.list().subscribe({
      next: (branches) => this.branches.set(branches.map((b) => ({ label: b.storeName, value: b.id }))),
    });
  }

  private loadProducts(): void {
    this.productApi.list({ limit: 100 }).subscribe({
      next: (res) => this.products.set(res.items),
    });
  }

  addItem(): void {
    this.form.items.push({ product_id: '', quantity: 1 });
  }

  removeItem(index: number): void {
    this.form.items.splice(index, 1);
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
      items: this.form.items,
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
      items: [] as BundleItemRequest[],
    };
  }
}
