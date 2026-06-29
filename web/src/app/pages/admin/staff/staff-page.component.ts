import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ToolbarModule } from 'primeng/toolbar';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AuthStore } from '../../../core/auth/auth.store';
import { StaffApiService } from '../../../core/services/staff-api.service';
import { BranchApiService } from '../../../core/services/branch-api.service';
import { StaffMember } from '../../../core/models/staff.model';
import { StaffFormDialogComponent } from './staff-form-dialog.component';

@Component({
  selector: 'app-staff-page',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, TableModule, TagModule,
    SelectModule, InputTextModule, IconFieldModule, InputIconModule,
    ToolbarModule, ConfirmDialogModule, ToastModule, TooltipModule,
    StaffFormDialogComponent
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './staff-page.component.html',
  styleUrl: './staff-page.component.scss'
})
export class StaffPageComponent implements OnInit {
  private readonly authStore = inject(AuthStore);
  private readonly staffApi = inject(StaffApiService);
  private readonly branchApi = inject(BranchApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly canCreate = computed(() => this.authStore.hasPermission('staff:create'));
  protected readonly canUpdate = computed(() => this.authStore.hasPermission('staff:update'));
  protected readonly canDelete = computed(() => this.authStore.hasPermission('staff:delete'));

  protected readonly staffMembers = signal<StaffMember[]>([]);
  protected readonly branches = signal<{ id: string; name: string }[]>([]);
  protected readonly loading = signal(false);
  protected readonly totalCount = signal(0);
  protected readonly pageSize = 10;

  protected filterSearch = '';
  protected filterBranchId: string | null = null;
  protected filterRoleCode: string | null = null;

  protected readonly branchOptions = computed(() =>
    this.branches().map((b) => ({ label: b.name, value: b.id }))
  );

  protected readonly roleOptions = [
    { label: 'Staff', value: 'staff' },
    { label: 'Manager', value: 'manager' },
  ];

  protected readonly dialogVisible = signal(false);
  protected readonly selectedStaff = signal<StaffMember | null>(null);

  ngOnInit(): void {
    this.branchApi.list().subscribe({
      next: (branches) => this.branches.set(branches.map((b) => ({ id: b.id, name: b.storeName }))),
    });
  }

  loadStaff(event: any): void {
    const offset = event.first ?? 0;
    const limit = event.rows ?? this.pageSize;
    this.loading.set(true);

    const filter: Record<string, any> = { limit, offset };
    if (this.filterSearch) filter['search'] = this.filterSearch;
    if (this.filterBranchId) filter['branch_id'] = this.filterBranchId;
    if (this.filterRoleCode) filter['role_code'] = this.filterRoleCode;

    this.staffApi.list(filter).subscribe({
      next: (res) => {
        this.staffMembers.set(res.items);
        this.totalCount.set(res.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  applyFilters(): void {
    this.loadStaff({ first: 0, rows: this.pageSize });
  }

  roleSeverity(roleCode: string): 'info' | 'warn' {
    switch (roleCode) {
      case 'manager': return 'warn';
      case 'staff': return 'info';
      default: return 'info';
    }
  }

  openCreateDialog(): void {
    this.selectedStaff.set(null);
    this.dialogVisible.set(true);
  }

  openEditDialog(member: StaffMember): void {
    this.selectedStaff.set(member);
    this.dialogVisible.set(true);
  }

  onDialogClose(saved: boolean): void {
    this.dialogVisible.set(false);
    this.selectedStaff.set(null);
    if (saved) {
      this.messageService.add({ severity: 'success', summary: 'Exito', detail: 'Personal guardado correctamente' });
      this.applyFilters();
    }
  }

  confirmDelete(member: StaffMember): void {
    this.confirmationService.confirm({
      message: `Eliminar a "${member.profileName}" (${member.email})?`,
      header: 'Confirmar eliminacion',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.staffApi.delete(member.userId).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Exito', detail: 'Personal eliminado' });
            this.applyFilters();
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Error', detail: 'No se pudo eliminar' });
          },
        });
      },
    });
  }
}
