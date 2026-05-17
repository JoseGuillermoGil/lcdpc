# Tasks Frontend: Manejo de Usuarios y Administradores

> Alcance actual frontend: solo admin global. Casos de multi-sede (`admin_sede`) se planifican en una fase posterior.

## Fase F1 - Base de autenticación web

- [ ] Configurar cliente HTTP con `credentials: include` para cookies `httpOnly`.
- [ ] Implementar bootstrap de sesión (`GET /api/v1/auth/me`) al iniciar la app.
- [ ] Crear store de sesión (`authenticated`, `userSummary`, `permissions`, `expiresInSeconds`).
- [ ] Manejar estados de sesión (`loading`, `authenticated`, `anonymous`, `expired`).
- [ ] Implementar estrategia de refresh silencioso con `POST /api/v1/auth/refresh`.

## Fase F2 - Flujos de autenticación UX

- [ ] Pantalla de login cliente conectada a `POST /api/v1/auth/login`.
- [ ] Flujo de logout con limpieza de estado local + redirección.
- [ ] Flujo de registro por pasos (email -> OTP -> perfil -> confirmación).
- [ ] Botón "Continuar con Google" que redirige a `GET /api/v1/auth/register/google`.
- [ ] Pantalla/handler de callback para consumir `GET /api/v1/auth/register/google/callback`.
- [ ] Prefill de formulario paso 2 usando `prefill` (`email`, `emailVerified`, `name`, `givenName`, `familyName`, `picture`, `locale`).
- [ ] Vista `forgot-password` conectada a `POST /api/v1/auth/forgot-password`.
- [ ] Vista `reset-password` conectada a `POST /api/v1/auth/reset-password`.
- [ ] Mensajería UX para errores comunes (`INVALID_CREDENTIALS`, `INVALID_SESSION`, `INVALID_OR_EXPIRED_RESET_TOKEN`).

## Fase F3 - Autorización en cliente

- [ ] Implementar guards de ruta por `permissions.canView`.
- [ ] Implementar helper de acciones (`canWrite`, `canUpdate`, `canDelete`, `canAll`).
- [ ] Renderizar menú dinámico según recursos visibles.
- [ ] Bloquear navegación/acciones cuando backend responde `USER_NOT_ALLOWED`.
- [ ] Crear componente `Forbidden` (403) reutilizable.

## Fase F4 - Dashboard seguridad auth

- [ ] Crear pantalla para ver `GET /api/v1/auth/security-policy`.
- [ ] Crear formulario para editar política (`passwordResetTtlMinutes`, `revokeSessionsOnPasswordReset`).
- [ ] Ocultar edición si el usuario no posee rol `admin_global`.
- [ ] Mostrar feedback de guard backend en 401/403.

## Fase F5 - Calidad frontend

- [ ] Tests unitarios de store de sesión y helpers de permisos.
- [ ] Tests de integración de guards de rutas.
- [ ] Tests E2E de login/logout/refresh.
- [ ] Tests E2E de forgot/reset password.
- [ ] Tests E2E de pantalla de `security-policy` con rol permitido/no permitido.

## Convención de mantenimiento

- [ ] Actualizar este archivo en cada iteración frontend (marcar checkboxes y dejar nota en PR).
