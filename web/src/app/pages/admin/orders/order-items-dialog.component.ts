import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { FloatLabelModule } from 'primeng/floatlabel';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { OrderApiService } from '../../../core/services/order-api.service';
import { ProductApiService } from '../../../core/services/product-api.service';
import { PriceApiService } from '../../../core/services/price-api.service';
import { PriceCategoryApiService } from '../../../core/services/price-category-api.service';
import { Product } from '../../../core/models/product.model';
import { PriceCategory } from '../../../core/models/price-category.model';
import { ProductBranchPrice } from '../../../core/models/price.model';
import {
  Order,
  ORDER_STATUS_LABELS,
  ORDER_STATUS_SEVERITY,
  ORDER_EDITABLE_STATUSES,
  UpdateOrderRequest,
} from '../../../core/models/order.model';

interface EditableOrderItem {
  id?: string;
  productId: string;
  quantity: number;
  originalQuantity: number;
  unitPrice: number;
  subtotal: number;
  priceOptions: { label: string; value: number }[];
  isNew?: boolean;
  isRemoved?: boolean;
}

@Component({
  selector: 'app-order-items-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, DialogModule,
    InputTextModule, InputNumberModule, SelectModule, FloatLabelModule,
    TagModule, ToastModule, TooltipModule,
  ],
  providers: [MessageService],
  templateUrl: './order-items-dialog.component.html',
  styleUrl: './order-items-dialog.component.scss'
})
export class OrderItemsDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() order: Order | null = null;
  @Input() branches: { id: string; name: string }[] = [];

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();

  private readonly orderApi = inject(OrderApiService);
  private readonly productApi = inject(ProductApiService);
  private readonly priceApi = inject(PriceApiService);
  private readonly priceCategoryApi = inject(PriceCategoryApiService);
  private readonly messageService = inject(MessageService);

  protected readonly saving = signal(false);
  protected readonly products = signal<Product[]>([]);
  protected readonly priceCategories = signal<PriceCategory[]>([]);
  protected items: EditableOrderItem[] = [];
  protected notes = '';
  protected submitted = false;

  protected get isEditable(): boolean {
    return !!ORDER_EDITABLE_STATUSES[this.order?.status ?? ''];
  }

  protected get visibleItems(): EditableOrderItem[] {
    return this.items.filter((i) => !i.isRemoved);
  }

  protected get total(): number {
    return this.visibleItems.reduce((sum, item) => sum + (item.quantity * item.unitPrice), 0);
  }

  protected get totalItems(): number {
    return this.visibleItems.reduce((sum, item) => sum + item.quantity, 0);
  }

  protected get duplicateProductIds(): Set<string> {
    const seen = new Set<string>();
    const dupes = new Set<string>();
    for (const item of this.visibleItems) {
      if (item.productId && seen.has(item.productId)) {
        dupes.add(item.productId);
      }
      seen.add(item.productId);
    }
    return dupes;
  }

  protected get hasDuplicates(): boolean {
    return this.duplicateProductIds.size > 0;
  }

  protected get hasStockIssues(): boolean {
    return this.visibleItems.some((item) => {
      if (!item.productId) return false;
      const product = this.products().find((p) => p.productId === item.productId);
      if (!product) return false;
      const effectiveBlocked = product.stockBlocked - item.originalQuantity + item.quantity;
      return effectiveBlocked > product.stock;
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible && this.order) {
      this.submitted = false;
      this.notes = this.order.notes ?? '';
      this.items = [];

      this.productApi.list({ branch_id: this.order.branchId, limit: 100 }).subscribe({
        next: (res) => this.products.set(res.items.filter((p) => p.isActive)),
      });

      this.priceCategoryApi.list().subscribe({
        next: (cats) => this.priceCategories.set(cats),
      });

      if (this.order.items && this.order.items.length > 0) {
        for (const item of this.order.items) {
          const productId = item.productId ?? '';
          const editable: EditableOrderItem = {
            id: item.id,
            productId,
            quantity: item.quantity,
            originalQuantity: item.quantity,
            unitPrice: item.unitPrice,
            subtotal: item.subtotal,
            priceOptions: [],
          };
          this.items.push(editable);
          if (productId) {
            this.loadPricesForItem(this.items.length - 1, productId);
          }
        }
      }
    }
  }

  private loadPricesForItem(index: number, productId: string): void {
    this.priceApi.listByProductId(productId).subscribe({
      next: (prices) => {
        if (index < this.items.length) {
          this.items[index].priceOptions = this.formatPriceOptions(prices);
        }
      },
    });
  }

  private formatPriceOptions(prices: ProductBranchPrice[]): { label: string; value: number }[] {
    return prices.map((p) => {
      const cat = this.priceCategories().find((c) => c.id === p.priceCategoryId);
      const name = cat?.name ?? 'Precio base';
      return { label: `${name} — $${p.amount.toFixed(2)}`, value: p.amount };
    });
  }

  protected addItem(): void {
    this.items.push({
      productId: '',
      quantity: 1,
      originalQuantity: 0,
      unitPrice: 0,
      subtotal: 0,
      priceOptions: [],
      isNew: true,
    });
  }

  protected removeItem(index: number): void {
    const item = this.visibleItems[index];
    const realIndex = this.items.indexOf(item);
    if (item.id) {
      this.items[realIndex].isRemoved = true;
    } else {
      this.items.splice(realIndex, 1);
    }
  }

  protected onProductSelect(index: number): void {
    const item = this.visibleItems[index];
    const realIndex = this.items.indexOf(item);
    if (!item.productId) return;

    this.items[realIndex].unitPrice = 0;
    this.items[realIndex].priceOptions = [];
    this.loadPricesForItem(realIndex, item.productId);

    this.priceApi.listByProductId(item.productId).subscribe({
      next: (prices) => {
        if (prices.length > 0 && this.items[realIndex].unitPrice === 0) {
          this.items[realIndex].unitPrice = prices[0].amount;
        }
      },
    });
  }

  protected isDuplicateItem(visibleIndex: number): boolean {
    const item = this.visibleItems[visibleIndex];
    return !!item.productId && this.duplicateProductIds.has(item.productId);
  }

  protected isOverStock(visibleIndex: number): boolean {
    const item = this.visibleItems[visibleIndex];
    if (!item.productId) return false;
    const product = this.products().find((p) => p.productId === item.productId);
    if (!product) return false;
    const effectiveBlocked = product.stockBlocked - item.originalQuantity + item.quantity;
    return effectiveBlocked > product.stock;
  }

  protected getItemStockInfo(visibleIndex: number): string {
    const item = this.visibleItems[visibleIndex];
    if (!item.productId) return '';
    const product = this.products().find((p) => p.productId === item.productId);
    if (!product) return '';
    const available = product.stock - product.stockBlocked;
    return `Disponible: ${available} | Bloqueado: ${product.stockBlocked} | Total: ${product.stock}`;
  }

  protected getProductName(productId: string): string {
    return this.products().find((p) => p.productId === productId)?.name ?? productId.slice(0, 8);
  }

  protected orderStatusLabel(status: string): string {
    return ORDER_STATUS_LABELS[status] ?? status;
  }

  protected orderStatusSeverity(status: string): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
    return ORDER_STATUS_SEVERITY[status] ?? 'info';
  }

  protected save(): void {
    this.submitted = true;
    if (!this.order) return;

    const activeItems = this.visibleItems;
    if (activeItems.length === 0) return;
    if (activeItems.some((i) => !i.productId || i.quantity <= 0)) return;
    if (this.hasDuplicates) return;
    if (this.hasStockIssues) return;

    this.saving.set(true);

    const req: UpdateOrderRequest = {
      notes: this.notes,
      items: activeItems.map((i) => ({
        item_type: 'product',
        product_id: i.productId,
        quantity: i.quantity,
        unit_price: i.unitPrice,
      })),
    };

    this.orderApi.update(this.order.id, req).subscribe({
      next: () => {
        this.saving.set(false);
        this.messageService.add({ severity: 'success', summary: 'Exito', detail: 'Orden actualizada' });
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        const msg = err?.error?.message ?? 'No se pudo actualizar la orden';
        this.messageService.add({ severity: 'error', summary: 'Error', detail: msg });
      },
    });
  }

  protected close(): void {
    this.visibleChange.emit(false);
  }
}
