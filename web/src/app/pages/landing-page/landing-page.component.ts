import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { BranchesComponent } from '../../shared/branches/branches.component';
import { CatalogComponent } from '../../shared/catalog/catalog.component';
import { HeroComponent } from '../../shared/hero/hero.component';

type HeroSlide = {
  title: string;
  imageUrl: string;
  alt: string;
};

type Category = {
  id: string;
  label: string;
};

type ProductCard = {
  id: string;
  name: string;
  price: string;
  description: string;
  imageUrl: string;
  alt: string;
  category: string;
  badge?: string;
  featured?: boolean;
  quantity: number;
};

type BranchCard = {
  id: string;
  name: string;
  address: string;
  phone: string;
  icon: string;
};

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [CommonModule, HeroComponent, CatalogComponent, BranchesComponent],
  templateUrl: './landing-page.component.html'
})
export class LandingPageComponent {
  private readonly router = inject(Router);

  protected readonly activeHeroIndex = signal(0);
  protected readonly search = signal('');
  protected readonly selectedCategoryId = signal('combos');

  protected readonly heroSlides: HeroSlide[] = [
    {
      title: '500 GRS de Nuggets 1 Kg Papas Salchichas',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuBBoEgsJ6MOR1J_3qJRGLiCwkc_qkteFtduHfbt3P3-FwUexh1tMYz0Hy5ywgxNj1LzdLx951ChpwPPnPApY-uiIQB12LrIcx5A-zxuABp8WMurCPbd7toIzIggUOvgRVELMDDDTk3tsCUvWtyBohLPxP9goQLftIPs52fJLOKddVVGeG23CyHrLoAtElndP87qwrOCZd2Xgr4ukWZBLkdlwvmrfHd2FEcVXIAG5L3uwkT4mr9C8AVBZXWvEySVoAriOHvvuKVkGhU',
      alt: 'Promoción principal de ingredientes para perros calientes'
    },
    {
      title: 'Combo para emprendedores con precio mayorista',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuBzDseBhHu9EwvRAco9ePAKN5P7xc9lx9fVoTnM1edHkFtu_nDMQr2z-_cRfekhrcHfOsJ_W_buOJqgVb_OLp4UoqKPBEVhA6IMMxYyEzkCUXOxGEbY5hFUMU0uelIITl_FMyarwcgYh9jWX3voMm8b7UAZD56M8Lp1wv_G-ELO4GrfqtnS5IKepgNLU6prrPnJmoHkSBPP3CJTkFgVkigEbMfXbd4i2mp8PTN_OMNFCjv-SSEVZsbNNt1yMDBcbYATQYyIFK63k7A',
      alt: 'Combo emprendedor con salchichas, panes y salsas'
    },
    {
      title: 'Catálogo mayorista con producto de alta rotación',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuCtjIPGFwHKbCB9WVJ-opSccsbmZktXss9K_ruOrt4T4IgGn540li3OvT1CyGSw7LIc46VNpyxxsEjJm6LHKIKg8E_QnDewh7g6k5k7ZHeQxxdAZDUIac2uvVIXBwCoouE180Fe-HvYuPW13gB3zRHA_kHGV1WPdLGYwffDJOAT7VXDLbl_q5CY2UtDTrDHbeWCk41n7wXXnIAeD1vRotyuj79aw41K_6Nl3bRf6bsdWSln08t-mGT2p9etEKdbAHBy8dTPKyznxJ0',
      alt: 'Salchichas al vacío para catálogo mayorista'
    }
  ];

  protected readonly categories: Category[] = [
    { id: 'combos', label: 'Combos' },
    { id: 'salchichas', label: 'Salchichas' },
    { id: 'panes', label: 'Panes' },
    { id: 'salsas', label: 'Salsas' }
  ];

  protected readonly products: ProductCard[] = [
    {
      id: 'pan-basico',
      name: 'Pan Básico x20',
      price: '$4.50',
      description: 'Paquete mayorista de 20 unidades de pan suave tradicional. Ideal para carritos y eventos.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuBkb7EcPzI89vSjQn2HVFk8-uzrTrT8wpYH-p_b3LY3c2iclAC9OeNaT21wDSDXNCY0ksoy2eQsh9UOQ3rjeCWnchAqF0zUQqG791ivNSi-ErlniTHMXD631C9yq1nw-HfkYl0vWGIJgZ-0Hx0HfDTsyO2g47NnlEMy3nUSaQOVtyV4uDbUutK94RosLm2llimN01s3lf_57e2XeqN3BhOFabOoByDC9WvzmHDmYuPR_PZiLrpQ5Z3GwiEitd4wvw2J2Xwl5u-pCDI',
      alt: 'Paquete de pan para perro caliente',
      category: 'panes',
      badge: 'Más Vendido',
      quantity: 10
    },
    {
      id: 'combo-emprendedor',
      name: 'Combo Emprendedor',
      price: '$25',
      description: '50 panes tradicionales, 50 salchichas tipo viena, 1 kg de papas fritas y 3 salsas.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuBzDseBhHu9EwvRAco9ePAKN5P7xc9lx9fVoTnM1edHkFtu_nDMQr2z-_cRfekhrcHfOsJ_W_buOJqgVb_OLp4UoqKPBEVhA6IMMxYyEzkCUXOxGEbY5hFUMU0uelIITl_FMyarwcgYh9jWX3voMm8b7UAZD56M8Lp1wv_G-ELO4GrfqtnS5IKepgNLU6prrPnJmoHkSBPP3CJTkFgVkigEbMfXbd4i2mp8PTN_OMNFCjv-SSEVZsbNNt1yMDBcbYATQYyIFK63k7A',
      alt: 'Combo promocional para emprendedores',
      category: 'combos',
      badge: 'Promo Especial',
      featured: true,
      quantity: 1
    },
    {
      id: 'salchicha-viena',
      name: 'Salchicha Viena x50',
      price: '$12.00',
      description: 'Empaque al vacío con 50 unidades de salchicha tipo viena estándar. Larga duración.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuCtjIPGFwHKbCB9WVJ-opSccsbmZktXss9K_ruOrt4T4IgGn540li3OvT1CyGSw7LIc46VNpyxxsEjJm6LHKIKg8E_QnDewh7g6k5k7ZHeQxxdAZDUIac2uvVIXBwCoouE180Fe-HvYuPW13gB3zRHA_kHGV1WPdLGYwffDJOAT7VXDLbl_q5CY2UtDTrDHbeWCk41n7wXXnIAeD1vRotyuj79aw41K_6Nl3bRf6bsdWSln08t-mGT2p9etEKdbAHBy8dTPKyznxJ0',
      alt: 'Paquete de salchichas tipo viena',
      category: 'salchichas',
      quantity: 5
    }
  ];

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

  protected readonly filteredProducts = computed(() => {
    const term = this.search().trim().toLowerCase();
    const category = this.selectedCategoryId();

    return this.products.filter((product) => {
      const matchesCategory = category === 'all' || product.category === category;
      const matchesTerm =
        term.length === 0 ||
        [product.name, product.description, product.category, product.badge ?? '']
          .join(' ')
          .toLowerCase()
          .includes(term);

      return matchesCategory && matchesTerm;
    });
  });

  protected selectCategory(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
  }

  protected goToAdvancedSearch(query: string): void {
    this.router.navigate(['/search'], {
      queryParams: query ? { q: query } : {}
    });
  }
}
