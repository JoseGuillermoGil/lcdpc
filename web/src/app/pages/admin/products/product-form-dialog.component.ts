import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { CheckboxModule } from 'primeng/checkbox';
import { FloatLabelModule } from 'primeng/floatlabel';
import { Product, CreateProductRequest } from '../../../core/models/product.model';
import { ProductApiService } from '../../../core/services/product-api.service';
import { BranchApiService } from '../../../core/services/branch-api.service';
import { CategoryStore } from '../../../core/stores/category.store';

@Component({
  selector: 'app-product-form-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, SelectModule,
    CheckboxModule, FloatLabelModule
  ],
  templateUrl: './product-form-dialog.component.html',
  styleUrl: './product-form-dialog.component.scss'
})
export class ProductFormDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() product: Product | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  private readonly productApi = inject(ProductApiService);
  private readonly branchApi = inject(BranchApiService);
  readonly categoryStore = inject(CategoryStore);

  protected readonly saving = signal(false);
  protected readonly branches = signal<{ label: string; value: string }[]>([]);
  protected submitted = false;
  protected imageFile: File | null = null;
  protected imagePreview: string | null = null;

  protected form: CreateProductRequest = this.emptyForm();

  protected readonly measureTypeOptions = [
    { label: 'Unidad', value: 'Unidad' },
    { label: 'Peso', value: 'Peso' },
    { label: 'Volumen', value: 'Volumen' },
  ];

  protected get isEditMode(): boolean {
    return this.product !== null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible) {
      this.loadBranches();
      if (this.product) {
        this.form = {
          name: this.product.name,
          sku: this.product.sku,
          base_measure_type: this.product.baseMeasureType,
          wholesale_commercial_type: this.product.wholesaleCommercialType,
          units_per_box: this.product.unitsPerBox,
          units_per_bundle: this.product.unitsPerBundle,
          category_id: this.product.categoryId,
          branch_id: this.product.branchId,
        };
        this.imagePreview = this.productApi.resolveImageUrl(this.product.img);
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
    if (!this.form.name || !this.form.sku || !this.form.base_measure_type || !this.form.wholesale_commercial_type) {
      return;
    }

    this.saving.set(true);

    const req: CreateProductRequest = {
      ...this.form,
      is_active: this.isEditMode ? this.product!.isActive : true,
    };

    const operation = this.isEditMode
      ? this.productApi.update(this.product!.productId, req, this.imageFile ?? undefined)
      : this.productApi.create(req, this.imageFile ?? undefined);

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

  private emptyForm(): CreateProductRequest {
    return {
      name: '',
      sku: '',
      base_measure_type: 'Unidad',
      wholesale_commercial_type: '',
      units_per_box: null,
      units_per_bundle: null,
      category_id: null,
      branch_id: null,
    };
  }
}
