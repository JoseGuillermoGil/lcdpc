import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

export function permissionGuard(...requiredPermissions: string[]): CanActivateFn {
  return () => {
    const store = inject(AuthStore);
    const router = inject(Router);

    if (!store.isAuthenticated()) {
      return router.createUrlTree(['/autenticacion']);
    }

    if (store.hasAnyPermission(...requiredPermissions)) {
      return true;
    }

    return router.createUrlTree(['/']);
  };
}
