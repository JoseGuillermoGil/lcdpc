import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { AuthStore } from '../../core/auth/auth.store';
import { SystemConfigStore } from '../../core/stores/system-config.store';

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
  readonly systemConfigStore = inject(SystemConfigStore);

  @Input() branches: HeaderBranch[] = [];
  @Input() selectedBranchId = '';
  @Input() cartCount = 0;

  @Output() branchChange = new EventEmitter<string>();
  @Output() cartClick = new EventEmitter<void>();

  protected readonly user = this.authStore.currentUser;
  protected readonly isAuthenticated = this.authStore.isAuthenticated;

  protected readonly isAdmin = computed(() =>
    this.authStore.hasAnyPermission(
      'product:create', 'product:update', 'product:delete',
      'bundle:create', 'bundle:update', 'bundle:delete',
      'order:view', 'order:create', 'order:update', 'order:delete',
      'rbac:resource:view',
      'staff:view'
    )
  );

  protected onLogoError(event: Event): void {
    (event.target as HTMLImageElement).src = '/not-found.png';
  }

  protected async logout(): Promise<void> {
    await this.authStore.logout().toPromise();
    await this.router.navigateByUrl('/');
  }
}
