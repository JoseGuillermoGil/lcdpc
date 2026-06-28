import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
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
  protected readonly allResults = signal<SearchResultItem[]>([]);

  protected readonly filteredResults = computed(() => {
    const q = this.query.trim().toLowerCase();
    if (!q) return this.allResults();
    return this.allResults().filter((item) =>
      [item.name, item.description, ...item.tags]
        .join(' ')
        .toLowerCase()
        .includes(q)
    );
  });

  ngOnInit(): void {
    forkJoin({
      categories: this.categoryApi.list(),
      products: this.productApi.list(),
      bundles: this.bundleApi.list()
    }).subscribe({
      next: ({ categories, products, bundles }) => {
        const categoryMap = new Map(categories.map((c) => [c.categoryId, c.name]));

        const bundleItems: SearchResultItem[] = bundles
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

        const productItems: SearchResultItem[] = products
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

        this.allResults.set([...bundleItems, ...productItems]);
      },
      error: () => {
        this.allResults.set([]);
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
  }
}
