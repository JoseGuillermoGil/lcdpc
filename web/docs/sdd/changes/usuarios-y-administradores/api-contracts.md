# API Contracts (Propuesta v1): Usuarios y Administradores

## Convenciones

- Base URL: `/api/v1`
- Auth (web): sesión por cookies `httpOnly`.
- Auth (no-web/integraciones): `Authorization: Bearer <accessToken>`.
- Cookies de sesión (web):
  - `lcdpc_at`: access token (`httpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, `Max-Age` corto).
  - `lcdpc_rt`: refresh token (`httpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/v1/auth`, `Max-Age` largo).
- Errores:
  - `400` validación
  - `401` no autenticado
  - `403` no autorizado
  - `404` no encontrado
  - `409` conflicto de negocio

---

## 1) Autenticación

### POST `/auth/register/start`

Inicia registro cliente (paso 1) y envía OTP de verificación al correo.

**Request**

```json
{
  "email": "usuario@empresa.com"
}
```

**Response 202**

```json
{
  "flowId": "uuid",
  "status": "pending_email_verification",
  "otpPolicy": {
    "ttlMinutes": 10,
    "maxAttempts": 5,
    "cooldownMinutes": 10
  }
}
```

### POST `/auth/register/verify-email`

Valida OTP del paso 1.

**Request**

```json
{
  "flowId": "uuid",
  "otp": "123456"
}
```

**Response 200**

```json
{
  "flowId": "uuid",
  "status": "pending_profile"
}
```

**Errores relevantes**

- `409 OTP_ATTEMPTS_EXCEEDED`: intentos agotados, debe esperar 10 minutos para nueva OTP.
- `410 OTP_EXPIRED`: OTP expirada.

### POST `/auth/register/profile`

Completa paso 2 con datos mínimos obligatorios.

**Request**

```json
{
  "flowId": "uuid",
  "nombres": "Juan",
  "apellidos": "Perez",
  "cedulaIdentidad": "V-12345678",
  "telefonoWhatsapp": "+584141112233",
  "direccionCompleta": "Calle 1, Edif 2, Ciudad"
}
```

**Response 201**

```json
{
  "usuarioId": "uuid",
  "estado": "activo",
  "tipoCuenta": "cliente"
}
```

### GET `/auth/register/google`

Inicia OAuth con Google (redirect al proveedor).

### GET `/auth/register/google/callback`

Callback de Google. Crea o reanuda flujo y prellena paso 2.

**Response 200**

```json
{
  "flowId": "uuid",
  "status": "pending_profile",
  "prefill": {
    "email": "juan@gmail.com",
    "emailVerified": true,
    "name": "Juan Pérez",
    "givenName": "Juan",
    "familyName": "Pérez",
    "picture": "https://...",
    "locale": "es-419"
  }
}
```

Campos de prefill alineados con claims estándar de proveedor OAuth 2.0/OpenID Connect (`email`, `email_verified`, `name`, `given_name`, `family_name`, `picture`, `locale`).

### POST `/auth/login`

Inicia sesión por email + contraseña y crea sesión persistente web por cookies `httpOnly`.

**Request**

```json
{
  "email": "usuario@empresa.com",
  "password": "SuperSecure#2026"
}
```

**Response 200**

```json
{
  "tokenPair": {
    "accessToken": "jwt",
    "refreshToken": "opaque-or-jwt",
    "expiresInSeconds": 3600
  },
  "userSummary": {
    "usuarioId": "uuid",
    "email": "usuario@empresa.com",
    "displayName": "Juan Perez",
    "estado": "activo",
    "tipoCuenta": "cliente",
    "onboardingStatus": "active",
    "emailVerifiedAtUtc": "2026-05-16T13:12:33Z",
    "roles": ["cliente"]
  },
  "permissions": [
    {
      "resourceCode": "catalog.search",
      "canView": true,
      "canWrite": false,
      "canUpdate": false,
      "canDelete": false,
      "canAll": false
    }
  ]
}
```

Headers esperados (web):

- `Set-Cookie: lcdpc_at=...; HttpOnly; Secure; SameSite=Lax; Path=/`
- `Set-Cookie: lcdpc_rt=...; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`

### GET `/auth/me`

Devuelve sesión actual para hidratar estado UI (menú, rutas y permisos), sin exponer internamente tokens en frontend.

**Response 200**

```json
{
  "authenticated": true,
  "userSummary": {
    "usuarioId": "uuid",
    "email": "usuario@empresa.com",
    "displayName": "Juan Perez",
    "estado": "activo",
    "tipoCuenta": "cliente",
    "onboardingStatus": "active",
    "emailVerifiedAtUtc": "2026-05-16T13:12:33Z",
    "roles": ["cliente"]
  },
  "permissions": [
    {
      "resourceCode": "catalog.search",
      "canView": true,
      "canWrite": false,
      "canUpdate": false,
      "canDelete": false,
      "canAll": false
    }
  ],
  "session": {
    "expiresInSeconds": 3120
  }
}
```

**Response 401**

```json
{
  "authenticated": false
}
```

### POST `/auth/refresh`

Renueva tokens.

**Request**

```json
{
  "refreshToken": "opaque-or-jwt"
}
```

**Response 200**

```json
{
  "accessToken": "jwt",
  "refreshToken": "opaque-or-jwt",
  "expiresInSeconds": 3600
}
```

### POST `/auth/logout`

Revoca la sesión actual.

**Response 204**

### POST `/auth/forgot-password`

Solicita recuperación para cliente y envía enlace/código al correo afiliado.

**Request**

```json
{
  "identifier": "usuario@empresa.com"
}
```

**Response 202**

```json
{
  "status": "recovery_requested"
}
```

**Errores relevantes**

- `404 AFFILIATED_EMAIL_NOT_FOUND`: cliente sin correo afiliado utilizable.
- `409 ACCOUNT_RECOVERY_NOT_ALLOWED`: cuenta no elegible para autoservicio.

### POST `/auth/reset-password`

Confirma nueva contraseña usando token/código de recuperación.

**Request**

```json
{
  "token": "reset-token",
  "newPassword": "SuperSecure#2026"
}
```

**Response 200**

```json
{
  "status": "password_updated",
  "sessionsRevoked": true
}
```

### POST `/admin/users/{usuarioId}/force-reset`

Reinicio de credenciales para usuarios admin, ejecutado por `admin_global` desde módulo de refrescamiento.

**Request**

```json
{
  "reason": "security-policy",
  "notifyEmail": true
}
```

**Response 202**

```json
{
  "status": "admin_reset_triggered"
}
```

---

## 2) Perfil de usuario

### GET `/users/me`

Obtiene perfil autenticado.

**Response 200**

```json
{
  "usuarioId": "uuid",
  "nombreCompleto": "Juan Perez",
  "telefono": "+584141112233",
  "email": "juan@email.com",
  "estado": "activo",
  "roles": ["cliente"]
}
```

### PATCH `/users/me`

Actualiza datos de perfil.

---

## 3) Administración de usuarios (solo `admin_global`)

### GET `/admin/users`

Lista usuarios con filtros.

**Query params**

- `q`
- `estado`
- `tipoCuenta`
- `rol`
- `page`
- `pageSize`

### POST `/admin/users`

Crea usuario administrador.

**Request**

```json
{
  "nombreCompleto": "Admin Sede",
  "telefono": "+584141119999",
  "email": "admin.sede@empresa.com",
  "rol": "admin_sede",
  "sedeIds": ["uuid-sede-1"]
}
```

### PATCH `/admin/users/{usuarioId}/status`

Activa, suspende o desactiva usuario.

**Request**

```json
{
  "estado": "activo"
}
```

### POST `/admin/users/{usuarioId}/roles`

Asigna rol y alcance por sede.

**Request**

```json
{
  "rol": "admin_sede",
  "sedeIds": ["uuid-sede-1", "uuid-sede-2"]
}
```

### DELETE `/admin/users/{usuarioId}/roles/{rolCodigo}`

Remueve rol.

---

## 4) Sedes y permisos

### GET `/admin/sedes`

Lista sedes.

### GET `/admin/me/permissions`

Devuelve capacidades efectivas del usuario autenticado.

**Response 200**

```json
{
  "usuarioId": "uuid",
  "roles": ["admin_sede"],
  "scope": {
    "type": "sede",
    "sedeIds": ["uuid-sede-1"]
  },
  "capabilities": [
    "inventory.read",
    "inventory.write",
    "pricing.read",
    "pricing.write",
    "combos.read",
    "combos.write",
    "admin.users.reset"
  ]
}
```

### GET `/admin/security/auth-policy`

Obtiene política de seguridad activa (OTP y reset tokens).

### PATCH `/admin/security/auth-policy`

Actualiza política de seguridad (módulo dashboard, roles autorizados).

**Request**

```json
{
  "otpTtlMinutes": 10,
  "otpMaxAttempts": 5,
  "otpCooldownMinutes": 10,
  "resetTokenTtlMinutes": 30,
  "revokeSessionsOnReset": true
}
```

---

## 5) Auditoría

### GET `/admin/audit-events`

Lista eventos auditables (admin_sede: solo sus sedes; admin_global: todo).

**Query params**

- `actorUsuarioId`
- `accion`
- `recursoTipo`
- `sedeId`
- `from`
- `to`
- `page`
- `pageSize`

**Response 200**

```json
{
  "items": [
    {
      "auditoriaId": "uuid",
      "actorUsuarioId": "uuid",
      "actorRol": "admin_sede",
      "accion": "pricing.updated",
      "recursoTipo": "precioProductoSede",
      "recursoId": "uuid",
      "sedeId": "uuid-sede-1",
      "timestamp": "2026-05-16T17:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

---

## 6) Reglas transversales

1. Endpoints `/admin/*` requieren token válido y rol administrativo.
2. `admin_sede` solo opera sobre recursos de sedes asignadas.
3. Cada mutación administrativa debe emitir evento de auditoría.
4. Usuario `desactivado` no puede iniciar ni renovar sesión.
5. OTP de registro: 5 intentos máximo, TTL 10 min, cooldown 10 min para nueva OTP al agotar intentos.
6. Recuperación de clientes usa correo afiliado de la cuenta.
7. Recuperación de admins es vía `admin_global` en módulo de refrescamiento.
8. TTL/revocación de reset token se configura en dashboard (`/admin/security/auth-policy`).
9. Cuentas con `authProvider=google` pueden requerir establecer credencial local según política antes de `forgot-password`.
10. Usuario con onboarding incompleto no puede operar checkout.

## 7) Open Questions (para cerrar en SDD)

1. ¿Permitimos login híbrido (Google + password local) desde v1 o en fase 2?
2. ¿MFA obligatorio para `admin_global` en v1?
3. ¿Política de expiración de refresh tokens (días y revocación global)?
4. ¿Necesitamos bloqueo por intentos fallidos (rate limit + cooldown adicional por IP/dispositivo)?
