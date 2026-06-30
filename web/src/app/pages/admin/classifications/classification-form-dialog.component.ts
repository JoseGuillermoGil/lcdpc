import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { FloatLabelModule } from 'primeng/floatlabel';
import { MeasurementUnitClassification, CreateMeasurementUnitClassificationRequest } from '../../../core/models/measurement-unit-classification.model';
import { MeasurementUnitClassificationApiService } from '../../../core/services/measurement-unit-classification-api.service';

@Component({
  selector: 'app-classification-form-dialog',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ButtonModule, DialogModule,
    InputTextModule, FloatLabelModule
  ],
  templateUrl: './classification-form-dialog.component.html',
  styleUrl: './classification-form-dialog.component.scss'
})
export class ClassificationFormDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() classification: MeasurementUnitClassification | null = null;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() saved = new EventEmitter<void>();
  @Output() closed = new EventEmitter<void>();

  private readonly classificationApi = inject(MeasurementUnitClassificationApiService);

  protected readonly saving = signal(false);
  protected submitted = false;

  protected form: CreateMeasurementUnitClassificationRequest = this.emptyForm();

  protected get isEditMode(): boolean {
    return this.classification !== null;
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['classification'] || changes['visible']) {
      if (this.visible && this.classification) {
        this.form = {
          name: this.classification.name,
          code: this.classification.code,
        };
      } else if (this.visible) {
        this.form = this.emptyForm();
      }
      this.submitted = false;
    }
  }

  save(): void {
    this.submitted = true;
    if (!this.form.name || !this.form.code) {
      return;
    }

    this.saving.set(true);

    const operation = this.isEditMode
      ? this.classificationApi.update(this.classification!.id, this.form)
      : this.classificationApi.create(this.form);

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

  private emptyForm(): CreateMeasurementUnitClassificationRequest {
    return {
      name: '',
      code: '',
    };
  }
}
