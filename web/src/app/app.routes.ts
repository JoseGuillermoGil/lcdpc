import { Routes } from '@angular/router';
import { AuthPageComponent } from './pages/auth-page/auth-page.component';
import { LandingPageComponent } from './pages/landing-page/landing-page.component';
import { RegisterPageComponent } from './pages/register-page/register-page.component';
import { SearchPageComponent } from './pages/search-page/search-page.component';

export const routes: Routes = [
	{ path: '', component: LandingPageComponent },
	{ path: 'search', component: SearchPageComponent },
	{ path: 'login', component: AuthPageComponent },
	{ path: 'register', component: RegisterPageComponent }
];
