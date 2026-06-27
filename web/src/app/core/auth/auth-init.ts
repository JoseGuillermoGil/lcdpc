import { inject } from '@angular/core';
import { AuthStore } from './auth.store';
import { catchError, of } from 'rxjs';

export function initializeAuth(): () => Promise<void> {
  return () => {
    const store = inject(AuthStore);
    return new Promise<void>((resolve) => {
      store.me().pipe(
        catchError(() => of(undefined))
      ).subscribe(() => {
        store.isLoaded.set(true);
        resolve();
      });
    });
  };
}
