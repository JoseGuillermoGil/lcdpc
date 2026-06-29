import { Routes } from '@angular/router';
import { AuthPageComponent } from './pages/auth-page/auth-page.component';
import { LandingPageComponent } from './pages/landing-page/landing-page.component';
import { RegisterPageComponent } from './pages/register-page/register-page.component';
import { SearchPageComponent } from './pages/search-page/search-page.component';
import { AdminLayoutComponent } from './pages/admin/admin-layout.component';
import { DashboardPageComponent } from './pages/admin/dashboard/dashboard-page.component';
import { ProductsPageComponent } from './pages/admin/products/products-page.component';
import { BundlesPageComponent } from './pages/admin/bundles/bundles-page.component';
import { OrdersPageComponent } from './pages/admin/orders/orders-page.component';
import { adminGuard } from './core/auth/admin.guard';
import { permissionGuard } from './core/auth/permission.guard';

export const routes: Routes = [
	{ path: '', component: LandingPageComponent },
	{ path: 'search', component: SearchPageComponent },
	{ path: 'login', component: AuthPageComponent },
	{ path: 'register', component: RegisterPageComponent },
	{
		path: 'admin',
		component: AdminLayoutComponent,
		canActivate: [adminGuard],
		children: [
			{ path: '', redirectTo: 'dashboard', pathMatch: 'full' },
			{ path: 'dashboard', component: DashboardPageComponent },
			{ path: 'products', component: ProductsPageComponent, canActivate: [permissionGuard('product:view')] },
			{ path: 'bundles', component: BundlesPageComponent, canActivate: [permissionGuard('bundle:view')] },
			{ path: 'orders', component: OrdersPageComponent, canActivate: [permissionGuard('order:view')] },
		]
	}
];
