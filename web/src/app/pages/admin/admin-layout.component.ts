import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss'
})
export class AdminLayoutComponent {
  private readonly authStore = inject(AuthStore);

  protected readonly collapsed = signal(true);

  protected readonly userName = computed(() => this.authStore.currentUser()?.displayName ?? 'Admin');

  protected readonly canViewProducts = computed(() =>
    this.authStore.hasPermission('product:view')
  );
  protected readonly canViewBundles = computed(() =>
    this.authStore.hasPermission('bundle:view')
  );
  protected readonly canViewOrders = computed(() =>
    this.authStore.hasPermission('order:view')
  );
  protected readonly canViewStaff = computed(() =>
    this.authStore.hasPermission('staff:view')
  );

  protected toggleSidebar(): void {
    this.collapsed.update((v) => !v);
  }
}
