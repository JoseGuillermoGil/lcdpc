import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { InputGroupModule } from 'primeng/inputgroup';
import { InputGroupAddonModule } from 'primeng/inputgroupaddon';
import { InputTextModule } from 'primeng/inputtext';

@Component({
  selector: 'app-catalog-search',
  standalone: true,
  imports: [CommonModule, ButtonModule, InputGroupModule, InputGroupAddonModule, InputTextModule],
  templateUrl: './catalog-search.component.html',
  styleUrl: './catalog-search.component.scss'
})
export class CatalogSearchComponent {
  @Input() search = '';

  @Output() searchChange = new EventEmitter<string>();
  @Output() searchSubmit = new EventEmitter<string>();

  protected onInput(value: string): void {
    this.searchChange.emit(value);
  }

  protected onSubmit(): void {
    this.searchSubmit.emit(this.search.trim());
  }
}
