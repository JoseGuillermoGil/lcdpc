import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ProductApiService } from '../../core/services/product-api.service';
import { BundleApiService } from '../../core/services/bundle-api.service';
import { CategoryApiService } from '../../core/services/category-api.service';
import { AdvancedSearchComponent, SearchResultItem } from '../../shared/advanced-search/advanced-search.component';
import { CatalogSearchComponent } from '../../shared/catalog-search/catalog-search.component';

const NOT_FOUND_IMAGE = '/not-found.png';

@Component({
  selector: 'app-search-page',
  standalone: true,
  imports: [CommonModule, CatalogSearchComponent, AdvancedSearchComponent],
  templateUrl: './search-page.component.html'
})
export class SearchPageComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly productApi = inject(ProductApiService);
  private readonly bundleApi = inject(BundleApiService);
  private readonly categoryApi = inject(CategoryApiService);

  protected query = this.route.snapshot.queryParamMap.get('q')?.trim() || '';
  protected readonly searchResults = signal<SearchResultItem[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    const search = this.query.trim();
    const filter = search ? { search, limit: 50 } : { limit: 50 };

    forkJoin({
      categories: this.categoryApi.list(),
      products: this.productApi.list(filter),
      bundles: this.bundleApi.list(filter)
    }).subscribe({
      next: ({ categories, products, bundles }) => {
        const categoryMap = new Map(categories.map((c) => [c.categoryId, c.name]));

        const bundleItems: SearchResultItem[] = bundles.items
          .filter((b) => b.status === 'Published')
          .map((b) => ({
            id: b.bundleId,
            name: b.name,
            price: '$0.00',
            description: `Código: ${b.code}`,
            imageUrl: this.bundleApi.resolveImageUrl(b.img) ?? NOT_FOUND_IMAGE,
            alt: b.name,
            tags: [b.categoryId ? (categoryMap.get(b.categoryId) ?? 'Combos') : 'Combos'],
            unitLabel: 'Precio Total Combo',
            featured: true
          }));

        const productItems: SearchResultItem[] = products.items
          .filter((p) => p.isActive)
          .map((p) => ({
            id: p.productId,
            name: p.name,
            price: '$0.00',
            description: `${p.baseMeasureType} — ${p.wholesaleCommercialType}`,
            imageUrl: this.productApi.resolveImageUrl(p.img) ?? NOT_FOUND_IMAGE,
            alt: p.name,
            tags: [p.categoryId ? (categoryMap.get(p.categoryId) ?? 'Productos') : 'Productos'],
            unitLabel: p.baseMeasureType
          }));

        this.searchResults.set([...bundleItems, ...productItems]);
      },
      error: () => {
        this.searchResults.set([]);
      }
    });
  }

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

    this.query = cleanQuery;
    this.loadData();
  }
}
