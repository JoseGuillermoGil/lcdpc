import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { AuthApiService } from './auth-api.service';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonModule, CheckboxModule, InputTextModule, PasswordModule],
  templateUrl: './auth-page.component.html',
  styleUrl: './auth-page.component.scss'
})
export class AuthPageComponent {
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);

  protected readonly username = signal('');
  protected readonly password = signal('');
  protected readonly rememberSession = signal(false);
  protected readonly isSubmitting = signal(false);
  protected readonly submitError = signal('');
  protected readonly canSubmit = computed(() => this.username().trim().length > 0 && this.password().trim().length > 0);

  protected async submit(): Promise<void> {
    this.submitError.set('');
    if (!this.canSubmit() || this.isSubmitting()) {
      return;
    }

    this.isSubmitting.set(true);

    try {
      const loginResponse = await firstValueFrom(this.authApi.login({
        email: this.username().trim(),
        password: this.password()
      }));

      await firstValueFrom(this.authApi.me(loginResponse.tokenPair.accessToken));
      await this.router.navigateByUrl('/');
    } catch (error) {
      this.submitError.set(this.resolveSubmitError(error));
    } finally {
      this.isSubmitting.set(false);
    }
  }

  private resolveSubmitError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 401) {
        return 'Credenciales inválidas. Verificá tu correo y contraseña.';
      }

      if (error.status === 403) {
        return 'Tu usuario no tiene acceso habilitado en este momento.';
      }

      if (error.status === 0) {
        return 'No se pudo conectar con el backend. Revisá que la API esté levantada.';
      }

      return 'No se pudo iniciar sesión. Intentá nuevamente.';
    }

    return 'Ocurrió un error inesperado. Intentá nuevamente.';
  }
}
