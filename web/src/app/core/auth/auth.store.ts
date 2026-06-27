import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, switchMap, tap, throwError } from 'rxjs';
import { AuthApiService } from '../../pages/auth-page/auth-api-go.service';

export interface UserSummary {
  id: string;
  email: string;
  displayName: string;
  estado: string;
  tipoCuenta: string;
  onboardingStatus: string;
  emailVerifiedAt: string | null;
  profileId: string;
}

interface MeGoData {
  authenticated: boolean;
  user: {
    id: string;
    email: string;
    display_name: string;
    estado: string;
    tipo_cuenta: string;
    onboarding_status: string;
    email_verified_at: string | null;
    profile_id: string;
  } | null;
  permissions: string[];
  expires_in: number | null;
}

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly authApi = inject(AuthApiService);

  readonly currentUser = signal<UserSummary | null>(null);
  readonly permissions = signal<string[]>([]);
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isLoaded = signal(false);
  readonly expiresAt = signal<number | null>(null);

  private refreshing = false;
  private refreshResult: Observable<boolean> | null = null;

  me(): Observable<void> {
    return this.authApi.me().pipe(
      map((data: MeGoData) => this.applyMeResponse(data)),
      catchError(() => {
        this.clear();
        return of(undefined);
      })
    );
  }

  login(email: string, password: string): Observable<void> {
    return this.authApi.login({ email, password }).pipe(
      tap((result) => {
        this.expiresAt.set(Date.now() + result.expiresIn * 1000);
      }),
      switchMap(() => this.me()),
      catchError((err) => {
        this.clear();
        return throwError(() => err);
      })
    );
  }

  refresh(): Observable<boolean> {
    if (this.refreshing && this.refreshResult) {
      return this.refreshResult;
    }

    this.refreshing = true;
    this.refreshResult = this.authApi.refresh().pipe(
      map((result) => {
        this.expiresAt.set(Date.now() + result.expiresIn * 1000);
        this.refreshing = false;
        this.refreshResult = null;
        return true;
      }),
      catchError(() => {
        this.clear();
        this.refreshing = false;
        this.refreshResult = null;
        return of(false);
      })
    );

    return this.refreshResult;
  }

  logout(): Observable<void> {
    return this.authApi.logout().pipe(
      tap(() => this.clear()),
      catchError(() => {
        this.clear();
        return of(undefined);
      })
    );
  }

  clear(): void {
    this.currentUser.set(null);
    this.permissions.set([]);
    this.expiresAt.set(null);
  }

  hasPermission(resourceCode: string): boolean {
    return this.permissions().includes(resourceCode);
  }

  hasAnyPermission(...resourceCodes: string[]): boolean {
    const perms = this.permissions();
    return resourceCodes.some((code) => perms.includes(code));
  }

  isExpiringSoon(): boolean {
    const exp = this.expiresAt();
    if (!exp) return false;
    return exp - Date.now() < 60_000;
  }

  private applyMeResponse(data: MeGoData): void {
    if (!data.authenticated || !data.user) {
      this.clear();
      return;
    }

    this.currentUser.set({
      id: data.user.id,
      email: data.user.email,
      displayName: data.user.display_name,
      estado: data.user.estado,
      tipoCuenta: data.user.tipo_cuenta,
      onboardingStatus: data.user.onboarding_status,
      emailVerifiedAt: data.user.email_verified_at,
      profileId: data.user.profile_id,
    });

    this.permissions.set(data.permissions ?? []);

    if (data.expires_in != null) {
      this.expiresAt.set(Date.now() + data.expires_in * 1000);
    }
  }
}
