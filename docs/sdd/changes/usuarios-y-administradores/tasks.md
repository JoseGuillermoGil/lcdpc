# Tasks: Manejo de Usuarios y Administradores

> Alcance actual: se manejará solo admin global; todo lo multi-sede (`admin_sede`) queda diferido a una fase futura.

## Fase 1 - Dominio y seguridad

- [x] Definir entidades: `Usuario`, `Rol`, `Permiso`, `Sesion`, `Auditoria`.
- [x] Definir entidad `Sede` y catálogo base de sedes (registro persistente).
- [x] Definir política RBAC v1 (`cliente`, `admin_sede`, `admin_global`).
- [x] Definir políticas de sesión (expiración, revocación, recuperación de acceso).
- [x] Definir y aplicar política OTP fija: 5 intentos, TTL 10 min, cooldown 10 min.
- [x] Definir `PoliticaSeguridadAuth` configurable en dashboard para reset token (TTL/revocación).

## Fase 2 - Autenticación

- [x] Implementar registro cliente por pasos (verificación correo -> perfil -> confirmación).
- [x] Implementar login cliente.
- [x] Unificar login único (clientes/admin) con opciones por permisos.
- [x] Implementar logout y renovación de sesión.
- [x] Implementar autenticación Google para iniciar/continuar registro.
- [x] Implementar prefill de paso 2 con claims OAuth 2.0/OpenID Connect (`email`, `email_verified`, `name`, `given_name`, `family_name`, `picture`, `locale`).
- [x] Endpoints de recuperación de clientes por correo afiliado (`forgot-password` / `reset-password`).
- [x] Login con `TokenPair` real + cookies `httpOnly` para web (`lcdpc_at`, `lcdpc_rt`).
- [x] Endpoint `GET /auth/me` para hidratar `UserSummary + permissions`.

## Fase 3 - Autorización

- [x] Middleware/guard por rol.
- [ ] (Diferido) Restricción por sedes para `admin_sede`.
- [x] Política de bloqueo para usuarios desactivados.
- [x] Resolver permisos efectivos por recurso para consumo de frontend.
- [ ] Guardas de rutas frontend basadas en `permissions.canView`.
- [ ] Menú dinámico frontend basado en recursos visibles.
- [ ] Guardas de acciones frontend (`canWrite`, `canUpdate`, `canDelete`, `canAll`).

## Fase 4 - Administración de usuarios

- [x] Pantalla/endpoint para registrar y listar sedes.
- [ ] Pantalla/endpoint para listar usuarios.
- [ ] Activar/desactivar usuario.
- [ ] Asignar/cambiar rol.
- [ ] (Diferido) Asignar sedes a `admin_sede`.
- [ ] Módulo de refrescamiento de usuarios admin (force reset) habilitado por rol.

## Fase 4.1 - Configuración de seguridad (Dashboard)

- [ ] Crear módulo de configuración de seguridad auth en dashboard.
- [x] Configurar TTL de reset token.
- [x] Configurar política de revocación de sesiones al reset.
- [x] Restringir edición de política por roles autorizados.

## Fase 5 - Auditoría

- [ ] Registrar auditoría en operaciones de inventario.
- [ ] Registrar auditoría en operaciones de precios.
- [ ] Registrar auditoría en operaciones de combos.
- [ ] Registrar cambios de roles/permisos.

## Fase 6 - QA

- [ ] Tests de autorización por rol.
- [ ] (Diferido) Tests de restricción por sede.
- [ ] Tests de login/logout/expiración.
- [ ] Tests de auditoría obligatoria en operaciones admin.
- [ ] Tests del flujo de alta por pasos.
- [x] Tests de onboarding con Google + prefill de paso 2.
- [ ] Tests de olvido y reseteo de contraseña.
- [ ] Tests de OTP (5 intentos / 10 min / cooldown 10 min).
- [ ] Tests de reset forzado de admins por `admin_global`.
- [ ] Tests de cambios de `auth-policy` en dashboard y su aplicación efectiva.
