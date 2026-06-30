import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { CategoriesSectionComponent } from './sections/categories-section.component';
import { PriceCategoriesSectionComponent } from './sections/price-categories-section.component';
import { MeasurementUnitsSectionComponent } from './sections/measurement-units-section.component';
import { RbacSectionComponent } from './sections/rbac-section.component';

type ConfigSection = 'profiles' | 'roles' | 'resources' | 'categories' | 'price-categories' | 'measurement-units';

interface SectionDef {
  key: ConfigSection;
  label: string;
  group: 'rbac' | 'inventario';
}

@Component({
  selector: 'app-config-page',
  standalone: true,
  imports: [
    CommonModule,
    CategoriesSectionComponent, PriceCategoriesSectionComponent,
    MeasurementUnitsSectionComponent, RbacSectionComponent,
  ],
  templateUrl: './config-page.component.html',
  styleUrl: './config-page.component.scss',
})
export class ConfigPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authStore = inject(AuthStore);

  protected readonly sections: SectionDef[] = [
    { key: 'profiles', label: 'Perfiles', group: 'rbac' },
    { key: 'roles', label: 'Roles', group: 'rbac' },
    { key: 'resources', label: 'Recursos', group: 'rbac' },
    { key: 'categories', label: 'Categorias', group: 'inventario' },
    { key: 'price-categories', label: 'Categorias de Precio', group: 'inventario' },
    { key: 'measurement-units', label: 'Unidades de Medida', group: 'inventario' },
  ];

  protected readonly activeSection = signal<ConfigSection>('categories');

  protected readonly currentSection = computed(() =>
    this.sections.find((s) => s.key === this.activeSection()) ?? this.sections[0]
  );

  protected readonly canViewRbac = computed(() =>
    this.authStore.hasPermission('rbac:profile:view')
  );

  protected readonly canViewInventario = computed(() =>
    this.authStore.hasAnyPermission('category:create', 'price_category:create', 'measurement_unit:create')
  );

  protected readonly visibleSections = computed(() => {
    const result: SectionDef[] = [];
    for (const s of this.sections) {
      if (s.group === 'rbac' && this.canViewRbac()) result.push(s);
      if (s.group === 'inventario' && this.canViewInventario()) result.push(s);
    }
    return result;
  });

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      const section = params.get('section') as ConfigSection | null;
      if (section && this.sections.some((s) => s.key === section)) {
        this.activeSection.set(section);
      } else {
        const first = this.visibleSections()[0];
        if (first) {
          this.activeSection.set(first.key);
          this.router.navigate([], {
            queryParams: { section: first.key },
            queryParamsHandling: 'merge',
            replaceUrl: true,
          });
        }
      }
    });
  }

  protected selectSection(key: ConfigSection): void {
    this.activeSection.set(key);
    this.router.navigate([], {
      queryParams: { section: key },
      queryParamsHandling: 'merge',
    });
  }

  protected getSectionTitle(): string {
    return this.currentSection()?.label ?? '';
  }
}
