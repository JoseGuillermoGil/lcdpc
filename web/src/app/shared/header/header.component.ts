import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { AuthStore } from '../../core/auth/auth.store';

export type HeaderBranch = {
  id: string;
  name: string;
};

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, SelectModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);

  @Input() branches: HeaderBranch[] = [];
  @Input() selectedBranchId = '';
  @Input() cartCount = 0;

  @Output() branchChange = new EventEmitter<string>();
  @Output() cartClick = new EventEmitter<void>();

  protected readonly user = this.authStore.currentUser;
  protected readonly isAuthenticated = this.authStore.isAuthenticated;

  protected async logout(): Promise<void> {
    await this.authStore.logout().toPromise();
    await this.router.navigateByUrl('/');
  }
}
