import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { FooterComponent } from './shared/footer/footer.component';
import { HeaderComponent } from './shared/header/header.component';
import { AuthStore } from './core/auth/auth.store';
import { BranchApiService } from './core/services/branch-api.service';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, HeaderComponent, FooterComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly branchApi = inject(BranchApiService);
  protected readonly authStore = inject(AuthStore);

  protected readonly showStoreShell = signal(!this.isAuthRoute(this.router.url));
  protected readonly selectedBranchId = signal('');
  protected readonly cartCount = signal(0);
  protected readonly branches = signal<{ id: string; name: string }[]>([]);

  constructor() {
    const subscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.showStoreShell.set(!this.isAuthRoute(event.urlAfterRedirects));
      });

    this.destroyRef.onDestroy(() => subscription.unsubscribe());
  }

  ngOnInit(): void {
    this.branchApi.list().subscribe({
      next: (branchList) => {
        const mapped = branchList.map((b) => ({ id: b.id, name: b.storeName }));
        this.branches.set(mapped);
        if (mapped.length > 0 && !this.selectedBranchId()) {
          this.selectedBranchId.set(mapped[0].id);
        }
      }
    });
  }

  protected selectBranch(branchId: string | null): void {
    if (branchId) {
      this.selectedBranchId.set(branchId);
    }
  }

  protected incrementCart(): void {
    this.cartCount.update((count) => count + 1);
  }

  private isAuthRoute(url: string): boolean {
    return url.startsWith('/login') || url.startsWith('/register');
  }
}
