import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AdvancedSearchComponent, SearchResultItem } from '../../shared/advanced-search/advanced-search.component';
import { CatalogSearchComponent } from '../../shared/catalog-search/catalog-search.component';

@Component({
  selector: 'app-search-page',
  standalone: true,
  imports: [CommonModule, CatalogSearchComponent, AdvancedSearchComponent],
  templateUrl: './search-page.component.html'
})
export class SearchPageComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected query = this.route.snapshot.queryParamMap.get('q')?.trim() || 'salchichas premium';

  protected readonly searchResults: SearchResultItem[] = [
    {
      id: 'salchicha-alemana',
      name: 'Salchicha Alemana Ahumada',
      price: '$32.50',
      description: 'Paquete mayorista de 50 unidades. Sabor intenso, tripa natural, ideal para asadores de alto tráfico.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuC6AaNTtaRK_zu9L-GLxqMA718dO2Ar524e6Y3cTetkiLHDL4pig3WLhLHL8OJuTEd8q2od51vcsNqLlM0OHObDUbWQrCx8li8fhhIdz657BYPfNYd8UxHEVC7OGWKBKyS5dq3bR9_YUyEQOmYpqWqfYmKNaij7IVUkOOY1cSBFcKGPGq5AedEd35xbCOKEaT2oS3DdjabjjCdB6njYRscNEVxiy3QYd_uJGbB8YAt-pcoLTbjw_MsZ2ZHk7q2Ze808vm_BtFSuVTk',
      alt: 'Salchicha alemana ahumada',
      tags: ['salchichas', 'granel'],
      unitLabel: 'Precio x Paquete',
      promo: 'Bestseller'
    },
    {
      id: 'pan-brioche',
      name: 'Pan Brioche Extra Suave',
      price: '$14.00',
      description: 'Bolsa de 24 unidades. Pan artesanal tipo brioche que soporta salsas pesadas sin romperse.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuDSMxxzqVTQRNIKVCSzNXw8iTEo15Qu6cgAW0uz4IL6j4rCv0F4NWm6QP5BvspUlD3FziJG6m9GRz-hYNBdKkOM8_NZNkROX_UGBjR_JV1IrqR1EEcAJGCYUMMEePmURHMmjA3ZjnjexeiioCGVxkagZ2M0F8EvWJEgDj8tYD3IpM7dGpU6X8tKDDIRGOVlqX9xYDzemzG6g1iCwEeOatoHuADyvuvipxz_kkthqDWM8uqK18zmXB88aFB5c2fiC1VfnLjyPn-Pf3c',
      alt: 'Pan brioche extra suave',
      tags: ['panes', 'premium'],
      unitLabel: 'Precio x Bolsa'
    },
    {
      id: 'combo-bbq',
      name: 'Kit Parrillero Alto Tráfico',
      price: '$85.00',
      oldPrice: '$105.00',
      description: '100 Salchichas + 100 Panes + 1 Galón de Salsa BBQ. Ideal para eventos y carritos en zonas concurridas.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuBQaIDPLJbIbolHluViVEHV8yKj7rnsjShNpQejCeaYd2FWhwdu_6GPMnvFN4FdQwtfW0M_Tqxo8kG6ZiyXLcyJCiHBErGImYhMAVMFTL_iIjTV0widkzBUlDDMlPtT8-Z9sMMWynkUWCzR5vrjbc0hH4MVMtsXzERQcSI_1nDgB2ePs77EFGkJuwE1KRDjUusJOsAWyVROzbkSX033tANVaPTC6yWXTDgUWeiPsX5mqIw3-BtG-0TfY4jvTYMPodg4TjdKaFik_kk',
      alt: 'Combo BBQ mayorista',
      tags: ['combo mayorista'],
      unitLabel: 'Precio Total Combo',
      featured: true,
      promo: 'Oferta'
    },
    {
      id: 'mostaza-dulce',
      name: 'Mostaza Dulce LCP (Galón)',
      price: '$18.20',
      description: 'Envase de 3.8 Litros. Receta exclusiva de la casa, equilibrio perfecto entre dulzor y acidez.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuBl2ejRYx0Gxys5xlMY3Gt4qmuwlFF52vkPsFi0BYNnqSWeS7mdXLvAR2s6r7j_Ad31cjyztSImTmBeVorhVTRhclCiapQya2e3HvLtCSMrwEglyBYaE-bVq8_yHY2YZ3XJ2A2W-LnmEpnU9SBQiph6L71A0xJtE7kdL8g-vnUtdOgTCUkqvmQO4psGoEKf8U3eJt6m6w8vbpJyg3KchkeNjBc8FLU4oh2yP5YYc9hwBVf5tLmSv5yHwmkbqFE8OJZZ2Ie5xiFY_-c',
      alt: 'Mostaza dulce LCP',
      tags: ['salsas'],
      unitLabel: 'Precio x Galón'
    },
    {
      id: 'salchicha-mixta',
      name: 'Salchicha Tradicional Mixta',
      price: '$22.00',
      description: 'Paquete de 100 unidades. La opción más rentable para alto volumen con excelente sabor base.',
      imageUrl:
        'https://lh3.googleusercontent.com/aida-public/AB6AXuCaqWOhYp3AOQx7o4rQr8rNwikpNsYKZvcIumOeHhIOVCEiBZRdJoakp2ZpnAmSV7zrzg85bmeOjsOXQgSC6Pk-kMaNnZQEcHssU87GPmgMaVGjPqJHtPeIZJKuGAt-FdwUPX5cgnL8xeB5OfqMkZoFXSM8dkgRWY7ANBJ1JbYkxBE3ic-yEVy7U2zrHBiNu0ElQ_2EVOkE7e63oGm91ch6tVzQ6iB3e4pwFHV7DiBZYWwtmLseXQDqCP91vIZE0-SjXTvq1MKmLSg',
      alt: 'Salchicha tradicional mixta',
      tags: ['salchichas', 'económica'],
      unitLabel: 'Precio x Paquete',
      soldOut: true
    }
  ];

  protected updateQuery(value: string): void {
    this.query = value;
  }

  protected submitSearch(query: string): void {
    const cleanQuery = query.trim();

    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: cleanQuery ? { q: cleanQuery } : {},
      replaceUrl: true
    });

    this.query = cleanQuery || 'salchichas premium';
  }
}
