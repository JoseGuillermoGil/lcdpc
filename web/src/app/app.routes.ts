import { Routes } from '@angular/router';
import { LandingPageComponent } from './pages/landing-page/landing-page.component';
import { SearchPageComponent } from './pages/search-page/search-page.component';

export const routes: Routes = [
	{ path: '', component: LandingPageComponent },
	{ path: 'busqueda', component: SearchPageComponent }
];
