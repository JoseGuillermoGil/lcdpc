import { Routes } from '@angular/router';
import { AuthPageComponent } from './pages/auth-page/auth-page.component';
import { LandingPageComponent } from './pages/landing-page/landing-page.component';
import { RegisterPageComponent } from './pages/register-page/register-page.component';
import { SearchPageComponent } from './pages/search-page/search-page.component';

export const routes: Routes = [
	{ path: '', component: LandingPageComponent },
	{ path: 'busqueda', component: SearchPageComponent },
	{ path: 'autenticacion', component: AuthPageComponent },
	{ path: 'registro', component: RegisterPageComponent }
];
