import { CommonModule } from '@angular/common';
import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from './shared/footer/footer.component';
import { HeaderComponent } from './shared/header/header.component';

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

  protected selectBranch(branchId: string | null): void {
    if (branchId) {
      this.selectedBranchId.set(branchId);
    }
  }

  protected incrementCart(): void {
    this.cartCount.update((count) => count + 1);
  }
}