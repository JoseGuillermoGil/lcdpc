import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { FooterComponent } from './shared/footer/footer.component';
import { HeaderComponent } from './shared/header/header.component';
import { AuthStore } from './core/auth/auth.store';

type BranchCard = {
  id: string;
  name: string;
  address: string;
  phone: string;
  icon: string;
};

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, HeaderComponent, FooterComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly authStore = inject(AuthStore);

  protected readonly showStoreShell = signal(!this.isAuthRoute(this.router.url));
  protected readonly selectedBranchId = signal('ocumare');
  protected readonly cartCount = signal(6);

  protected readonly branches: BranchCard[] = [
    {
      id: 'ocumare',
      name: 'Ocumare',
      address: 'Av. Principal de Ocumare del Tuy, Sector Centro.',
      phone: '+58 412-0000000',
      icon: 'storefront'
    },
    {
      id: 'charallave',
      name: 'Charallave',
      address: 'Calle Bolívar, frente a la plaza, Charallave.',
      phone: '+58 414-0000000',
      icon: 'storefront'
    },
    {
      id: 'santa-teresa',
      name: 'Santa Teresa',
      address: 'Av. Ayacucho, Santa Teresa del Tuy.',
      phone: '+58 424-0000000',
      icon: 'storefront'
    }
  ];

  constructor() {
    const subscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => {
        this.showStoreShell.set(!this.isAuthRoute(event.urlAfterRedirects));
      });

    this.destroyRef.onDestroy(() => subscription.unsubscribe());
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
