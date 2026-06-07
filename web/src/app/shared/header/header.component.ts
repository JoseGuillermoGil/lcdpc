import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';

export type HeaderBranch = {
  id: string;
  name: string;
};

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonModule, SelectModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  @Input() branches: HeaderBranch[] = [];
  @Input() selectedBranchId = '';
  @Input() cartCount = 0;

  @Output() branchChange = new EventEmitter<string>();
  @Output() cartClick = new EventEmitter<void>();
}
