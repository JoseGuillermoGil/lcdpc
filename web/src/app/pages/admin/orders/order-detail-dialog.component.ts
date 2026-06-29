import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output, signal, SimpleChanges } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { TableModule } from 'primeng/table';
import { Order } from '../../../core/models/order.model';
import { OrderApiService } from '../../../core/services/order-api.service';

@Component({
  selector: 'app-order-detail-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, TagModule, TableModule],
  templateUrl: './order-detail-dialog.component.html',
  styleUrl: './order-detail-dialog.component.scss'
})
export class OrderDetailDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() order: Order | null = null;
  @Input() branches: { id: string; name: string }[] = [];

  @Output() visibleChange = new EventEmitter<boolean>();

  private readonly orderApi = inject(OrderApiService);

  protected readonly history = signal<any[]>([]);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible'] && this.visible && this.order) {
      this.orderApi.getHistory(this.order.id).subscribe({
        next: (h) => this.history.set(h),
      });
    }
  }

  getBranchName(branchId: string): string {
    return this.branches.find((b) => b.id === branchId)?.name ?? branchId.slice(0, 8);
  }

  orderStatusSeverity(status: string): 'info' | 'success' | 'warn' | 'danger' {
    switch (status) {
      case 'PENDING_REVIEW': return 'warn';
      case 'APPROVED': return 'info';
      case 'IN_PREPARATION': return 'info';
      case 'READY': return 'success';
      case 'DELIVERED': return 'success';
      case 'REJECTED': return 'danger';
      case 'CANCELLED': return 'danger';
      default: return 'info';
    }
  }
}
