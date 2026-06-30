import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AuthStore } from '../../../core/auth/auth.store';
import { MeasurementUnitClassificationApiService } from '../../../core/services/measurement-unit-classification-api.service';
import { MeasurementUnitClassification } from '../../../core/models/measurement-unit-classification.model';
import { ClassificationFormDialogComponent } from './classification-form-dialog.component';

@Component({
  selector: 'app-classifications-page',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, TableModule,
    ConfirmDialogModule, ToastModule, ClassificationFormDialogComponent
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './classifications-page.component.html',
  styleUrl: './classifications-page.component.scss'
})
export class ClassificationsPageComponent implements OnInit {
  private readonly authStore = inject(AuthStore);
  private readonly classificationApi = inject(MeasurementUnitClassificationApiService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly canCreate = computed(() => this.authStore.hasPermission('product:create'));
  protected readonly canUpdate = computed(() => this.authStore.hasPermission('product:create'));
  protected readonly canDelete = computed(() => this.authStore.hasPermission('product:delete'));

  protected readonly classifications = signal<MeasurementUnitClassification[]>([]);
  protected readonly loading = signal(false);

  protected readonly dialogVisible = signal(false);
  protected readonly selectedClassification = signal<MeasurementUnitClassification | null>(null);

  ngOnInit(): void {
    this.loadClassifications();
  }

  loadClassifications(): void {
    this.loading.set(true);
    this.classificationApi.list().subscribe({
      next: (data) => {
        this.classifications.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openCreateDialog(): void {
    this.selectedClassification.set(null);
    this.dialogVisible.set(true);
  }

  openEditDialog(classification: MeasurementUnitClassification): void {
    this.selectedClassification.set(classification);
    this.dialogVisible.set(true);
  }

  onDialogClose(saved: boolean): void {
    this.dialogVisible.set(false);
    this.selectedClassification.set(null);
    if (saved) {
      this.messageService.add({ severity: 'success', summary: 'Exito', detail: 'Clasificacion guardada' });
      this.loadClassifications();
    }
  }

  confirmDelete(classification: MeasurementUnitClassification): void {
    this.confirmationService.confirm({
      message: `Eliminar la clasificacion "${classification.name}"?`,
      header: 'Confirmar eliminacion',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Eliminar',
      rejectLabel: 'Cancelar',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.classificationApi.delete(classification.id).subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: 'Exito', detail: 'Clasificacion eliminada' });
            this.loadClassifications();
          },
          error: () => {
            this.messageService.add({ severity: 'error', summary: 'Error', detail: 'No se pudo eliminar' });
          },
        });
      },
    });
  }
}
