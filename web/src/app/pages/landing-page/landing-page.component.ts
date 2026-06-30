import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ProductApiService } from '../../core/services/product-api.service';
import { BundleApiService } from '../../core/services/bundle-api.service';
import { CategoryStore } from '../../core/stores/category.store';
import { BranchApiService } from '../../core/services/branch-api.service';
import { BranchesComponent } from '../../shared/branches/branches.component';
import { CatalogComponent } from '../../shared/catalog/catalog.component';
import { HeroComponent } from '../../shared/hero/hero.component';

const NOT_FOUND_IMAGE = '/not-found.png';

type HeroSlide = {
  title: string;
  imageUrl: string;
  alt: string;
};

type ProductCard = {
  id: string;
  name: string;
  price: string;
  description: string;
  imageUrl: string;
  alt: string;
  categoryId: string;
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
export class LandingPageComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly productApi = inject(ProductApiService);
  private readonly bundleApi = inject(BundleApiService);
  readonly categoryStore = inject(CategoryStore);
  private readonly branchApi = inject(BranchApiService);

  protected readonly activeHeroIndex = signal(0);
  protected readonly search = signal('');
  protected readonly selectedCategoryId = signal('all');
  protected readonly products = signal<ProductCard[]>([]);
  protected readonly branches = signal<BranchCard[]>([]);

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

  protected readonly filteredProducts = computed(() => {
    const term = this.search().trim().toLowerCase();
    const category = this.selectedCategoryId();

    return this.products().filter((product) => {
      const matchesCategory = category === 'all' || product.categoryId === category;
      const matchesTerm =
        term.length === 0 ||
        [product.name, product.description, product.category, product.badge ?? '']
          .join(' ')
          .toLowerCase()
          .includes(term);

      return matchesCategory && matchesTerm;
    });
  });

  ngOnInit(): void {
    this.categoryStore.load();

    forkJoin({
      products: this.productApi.list(),
      bundles: this.bundleApi.list(),
      branches: this.branchApi.list()
    }).subscribe({
      next: ({ products, bundles, branches }) => {
        const bundleCards: ProductCard[] = bundles.items
          .filter((b) => b.status === 'Published')
          .map((b) => ({
            id: b.bundleId,
            name: b.name,
            price: '$0.00',
            description: `Código: ${b.code}`,
            imageUrl: this.bundleApi.resolveImageUrl(b.img) ?? NOT_FOUND_IMAGE,
            alt: b.name,
            categoryId: b.categoryId ?? '',
            category: this.categoryStore.getCategoryName(b.categoryId),
            featured: true,
            quantity: 1
          }));

        const productCards: ProductCard[] = products.items
          .filter((p) => p.isActive)
          .map((p) => ({
            id: p.productId,
            name: p.name,
            price: '$0.00',
            description: '',
            imageUrl: this.productApi.resolveImageUrl(p.img) ?? NOT_FOUND_IMAGE,
            alt: p.name,
            categoryId: p.categoryId ?? '',
            category: this.categoryStore.getCategoryName(p.categoryId),
            quantity: 1
          }));

        this.products.set([...bundleCards, ...productCards]);

        this.branches.set(
          branches.map((b) => ({
            id: b.id,
            name: b.storeName,
            address: b.address,
            phone: b.contactPhone,
            icon: 'storefront'
          }))
        );
      },
      error: () => {
        this.products.set([]);
        this.branches.set([]);
      }
    });
  }

  protected selectCategory(categoryId: string): void {
    this.selectedCategoryId.set(categoryId);
  }

  protected goToAdvancedSearch(query: string): void {
    this.router.navigate(['/search'], {
      queryParams: query ? { q: query } : {}
    });
  }
}
