# LCDPC API — Documentación Completa de Endpoints

Base URL: `http://localhost:8080`
Formato de respuesta: **JSend** (`{"status": "success|fail|error", "data": {}, "message": ""}`)

---

## 1. Health & Discovery

### `GET /`
Ping básico.
```json
{"service": "LCDPC.API", "status": "ok"}
```

### `GET /health`
Readiness probe. 200 = ok, 503 = db unreachable.
```
ok
```

### `GET /api/health`
Health check con DB ping.
```json
{"status": "success", "data": {"service": "LCDPC.API", "status": "ok"}}
```

### `GET /.well-known/openid-configuration`
Discovery document OAuth2/OpenID.
```json
{
  "issuer": "http://localhost:8080",
  "authorization_endpoint": "http://localhost:8080/oauth2/authorize",
  "token_endpoint": "http://localhost:8080/oauth2/token",
  "introspection_endpoint": "http://localhost:8080/oauth2/introspect",
  "revocation_endpoint": "http://localhost:8080/oauth2/revoke",
  "response_types_supported": ["code"],
  "grant_types_supported": ["authorization_code", "refresh_token"],
  "code_challenge_methods_supported": ["S256"],
  "subject_types_supported": ["public"]
}
```

---

## 2. OAuth2 Server

### `GET /oauth2/authorize`
Authorization Code + PKCE (S256).

**Query params:**
| Param | Requerido | Descripción |
|-------|-----------|-------------|
| `client_id` | Sí | ID del cliente OAuth2 registrado |
| `redirect_uri` | Sí | URI registrada del cliente |
| `response_type` | Sí | Solo `code` |
| `scope` | No | Scopes separados por espacio |
| `state` | Recomendado | CSRF protection |
| `code_challenge` | Si PKCE requerido | SHA256(code_verifier) en base64url |
| `code_challenge_method` | Si PKCE requerido | Solo `S256` |

**Auth:** Cookie `lcdpc_at` o `Authorization: Bearer <token>` (usuario logueado).

**Respuesta exitosa:** Redirect 302 a `{redirect_uri}?code={code}&state={state}`

**Respuesta error:** Redirect 302 a `{redirect_uri}?error={code}&error_description={msg}`

**Errores posibles:** `invalid_client`, `invalid_redirect_uri`, `unsupported_response_type`, `invalid_scope`, `invalid_request`, `login_required`

---

### `POST /oauth2/token`
Intercambia authorization code por tokens, o refresca un refresh token.

**Content-Type:** `application/x-www-form-urlencoded`

#### Grant type: `authorization_code`
```
grant_type=authorization_code
&client_id=lcdpc-web
&code={authorization_code}
&redirect_uri={redirect_uri}
&code_verifier={random_string}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "access_token": "v2.local...",
    "token_type": "Bearer",
    "expires_in": 3600,
    "refresh_token": "opaque-token",
    "scope": "openid email profile"
  }
}
```

#### Grant type: `refresh_token`
```
grant_type=refresh_token
&client_id=lcdpc-web
&refresh_token={refresh_token}
```

**Respuesta:** Igual que `authorization_code`.

**Errores posibles:** `invalid_client`, `invalid_grant`, `unsupported_grant_type`, `invalid_request`, `server_error`

---

### `POST /oauth2/introspect`
RFC 7662. Valida un token (access o refresh).

**Content-Type:** `application/x-www-form-urlencoded`

```
token={access_token_or_refresh_token}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "active": true,
    "client_id": "lcdpc-web",
    "username": "user@example.com",
    "scope": "openid email profile",
    "exp": 1750000000,
    "iat": 1749996400,
    "sub": "uuid-del-usuario"
  }
}
```

Si el token es inválido o expirado: `{"active": false}`

---

### `POST /oauth2/revoke`
RFC 7009. Revoca un refresh token y toda su familia.

**Content-Type:** `application/x-www-form-urlencoded`

```
token={refresh_token}
```

**Respuesta:** Siempre 200 (incluso si el token no existe).
```json
{"status": "success", "data": {}}
```

---

## 3. Autenticación (`/api/v1/auth`)

### `POST /api/v1/auth/register/start`
Inicia registro. Envía OTP al email.

**Request:**
```json
{"email": "user@example.com"}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "flow_id": "uuid",
    "status": "pending_email_verification",
    "otp_policy": {
      "ttl_minutes": 10,
      "max_attempts": 5,
      "cooldown_minutes": 10
    }
  }
}
```

**Errores:** `EMAIL_ALREADY_REGISTERED`, `OTP_COOLDOWN_ACTIVE`

---

### `POST /api/v1/auth/register/verify-email`
Verifica el OTP recibido por email.

**Request:**
```json
{
  "flow_id": "uuid-del-flow",
  "otp": "123456"
}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "flow_id": "uuid",
    "status": "pending_profile"
  }
}
```

**Errores:** `FLOW_NOT_FOUND`, `FLOW_INVALID_STATUS`, `OTP_EXPIRED`, `OTP_INVALID`, `OTP_ATTEMPTS_EXCEEDED`

---

### `POST /api/v1/auth/register/profile`
Completa el perfil y crea el usuario. Asigna rol `cliente`.

**Request:**
```json
{
  "flow_id": "uuid-del-flow",
  "nombres": "Juan",
  "apellidos": "Pérez",
  "documento_identidad": "V-12345678",
  "rif": "J-12345678-9",
  "telefono_whatsapp": "+584141234567",
  "direccion_completa": "Calle 1, Edif 2, Apt 3",
  "password": "MiPassword123!"
}
```

**Respuesta (201):**
```json
{
  "status": "success",
  "data": {
    "user_id": "uuid",
    "status": "activo",
    "tipo_cuenta": "cliente"
  }
}
```

**Errores:** `FLOW_NOT_FOUND`, `FLOW_INVALID_STATUS`, `EMAIL_ALREADY_REGISTERED`

---

### `POST /api/v1/auth/login`
Login con email/password. Genera PASETO access token + refresh token.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "MiPassword123!"
}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "access_token": "v2.local...",
    "refresh_token": "opaque-token",
    "expires_in": 3600
  }
}
```

**Cookies seteadas:**
- `lcdpc_at` — PASETO access token (HttpOnly, Lax)
- `lcdpc_rt` — Refresh token (HttpOnly, Strict)

**Headers de deprecación:**
- `Deprecation: true`
- `Sunset: Sat, 13 Sep 2026`

**Errores:** `INVALID_CREDENTIALS`, `ACCOUNT_INACTIVE`

**Frontend:** Usar `Authorization: Bearer {access_token}` en requests subsecuentes. Cookies son fallback para compatibilidad.

---

### `GET /api/v1/auth/me`
Retorna usuario autenticado actual.

**Auth:** Requerida (Bearer token o cookie `lcdpc_at`)

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "authenticated": true,
    "user": {
      "id": "uuid",
      "email": "user@example.com",
      "display_name": "Juan * Pérez",
      "estado": "activo",
      "tipo_cuenta": "cliente",
      "onboarding_status": "active",
      "email_verified_at": "2025-06-25T10:00:00Z",
      "roles": ["cliente"]
    },
    "permissions": [
      {
        "resource_code": "productos",
        "can_view": true,
        "can_write": false,
        "can_update": false,
        "can_delete": false,
        "can_all": false
      }
    ],
    "expires_in": 3542
  }
}
```

Si no autenticado: `{"authenticated": false, "user": null, "permissions": []}`

---

### `POST /api/v1/auth/refresh`
Renueva access token usando refresh token.

**Auth:** Requerida (Bearer token o cookie)

**Request (opcional, puede usar cookie):**
```json
{"refresh_token": "opaque-token"}
```

**Respuesta:** Igual que login.
**Errores:** `INVALID_REFRESH_TOKEN`

---

### `POST /api/v1/auth/logout`
Revoca la sesión actual.

**Auth:** Requerida

**Respuesta:**
```json
{"status": "success", "data": {"status": "logged_out"}}
```

Limpia cookies `lcdpc_at` y `lcdpc_rt`.

---

### `POST /api/v1/auth/forgot-password`
Genera token de reseteo de password.

**Request:**
```json
{"email": "user@example.com"}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "status": "accepted",
    "message": "if the account exists, a reset instruction has been generated"
  }
}
```

Siempre retorna 200 (no revela si el email existe).

---

### `POST /api/v1/auth/reset-password`
Resetea password con token recibido por email.

**Request:**
```json
{
  "token": "opaque-reset-token",
  "new_password": "NuevoPassword456!"
}
```

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "status": "completed",
    "sessions_revoked": true
  }
}
```

**Errores:** `INVALID_OR_EXPIRED_RESET_TOKEN`

---

### `GET /api/v1/auth/security-policy`
Consulta política de seguridad (público).

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "password_reset_ttl_minutes": 30,
    "revoke_sessions_on_password_reset": true
  }
}
```

---

### `PUT /api/v1/auth/security-policy`
Actualiza política de seguridad.

**Auth:** Requiere permiso `security-policy:update`

**Request:**
```json
{
  "password_reset_ttl_minutes": 60,
  "revoke_sessions_on_password_reset": true
}
```

**Errores:** `INVALID_RESET_TTL` (rango: 5-1440 minutos)

---

## 4. Productos (`/api/v1/productos`)

### `GET /api/v1/productos`
Lista todos los productos. **Público.**

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "producto_id": "uuid",
      "nombre": "Coca-Cola 2L",
      "sku": "CC-2L",
      "tipo_medida_base": "Unidad",
      "tipo_comercial_mayor": "Caja",
      "unidades_por_caja": 6,
      "unidades_por_bulto": 24,
      "activo": true
    }
  ]
}
```

---

### `GET /api/v1/productos/{id}`
Obtiene un producto por ID. **Público.**

**Respuesta:** Objeto producto individual.

---

### `POST /api/v1/productos`
Crea un producto.

**Auth:** Requiere permiso `product:create`

**Request:**
```json
{
  "nombre": "Coca-Cola 2L",
  "sku": "CC-2L",
  "tipo_medida_base": "Unidad",
  "tipo_comercial_mayor": "Caja",
  "unidades_por_caja": 6,
  "unidades_por_bulto": 24
}
```

**Validaciones:**
- Si `tipo_medida_base` = `"Unidad"`: `unidades_por_caja` y `unidades_por_bulto` requeridos y > 0
- `unidades_por_bulto` debe ser >= `unidades_por_caja`

**Respuesta (201):** Objeto producto.

---

### `PUT /api/v1/productos/{id}`
Actualiza un producto.

**Auth:** Requiere permiso `product:update`

**Request:** Igual que create.

---

### `DELETE /api/v1/productos/{id}`
Elimina un producto.

**Auth:** Requiere permiso `product:delete`

**Respuesta:**
```json
{"status": "success", "data": {"status": "deleted"}}
```

---

## 5. Combos (`/api/v1/combos`)

### `GET /api/v1/combos`
Lista todos los combos. **Público.**

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "combo_id": "uuid",
      "codigo": "COMBO-001",
      "nombre": "Combo Familiar",
      "estado": "Publicado",
      "sede_ids_habilitadas": ["uuid1", "uuid2"],
      "precio_total": 25.99,
      "precio_total_moneda": "USD",
      "precio_promocional": 19.99,
      "precio_promocional_moneda": "USD",
      "items": [
        {
          "id": "uuid",
          "combo_id": "uuid",
          "producto_id": "uuid-producto",
          "cantidad": 2
        }
      ]
    }
  ]
}
```

---

### `GET /api/v1/combos/{id}`
Obtiene un combo por ID con sus items. **Público.**

---

### `POST /api/v1/combos`
Crea un combo con items.

**Auth:** Requiere permiso `combo:create`

**Request:**
```json
{
  "codigo": "COMBO-001",
  "nombre": "Combo Familiar",
  "sede_ids_habilitadas": ["uuid1", "uuid2"],
  "items": [
    {"producto_id": "uuid", "cantidad": 2},
    {"producto_id": "uuid2", "cantidad": 1}
  ]
}
```

Estado inicial: `Borrador`. Precio total: `0` (se calcula aparte o se actualiza via sync).

---

### `PUT /api/v1/combos/{id}`
Actualiza combo. Reemplaza items existentes.

**Auth:** Requiere permiso `combo:update`

---

### `POST /api/v1/combos/{id}/publicar`
Cambia estado a `Publicado`.

**Auth:** Requiere permiso `combo:publish`

**Respuesta:** `{"status": "success", "data": {"status": "Publicado"}}`

---

### `POST /api/v1/combos/{id}/pausar`
Cambia estado a `Pausado`.

**Auth:** Requiere permiso `combo:pause`

**Respuesta:** `{"status": "success", "data": {"status": "Pausado"}}`

---

### `DELETE /api/v1/combos/{id}`
Elimina un combo.

**Auth:** Requiere permiso `combo:delete`

---

## 6. Precios (`/api/v1/precios`)

### `GET /api/v1/precios/{id}`
Obtiene un precio por ID. **Público.**

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "precio_producto_sede_id": "uuid",
    "producto_id": "uuid",
    "sede_id": "uuid",
    "precio1_unidad": 1.50,
    "precio1_moneda": "USD",
    "precio2_caja_bulto_pieza": 1.20,
    "precio2_moneda": "USD",
    "precio3_mayor_desde2": 1.00,
    "precio3_moneda": "USD",
    "precio4_mayorista": 0.85,
    "precio4_moneda": "USD",
    "precio4_requiere_acuerdo": false,
    "vigente_desde": "2025-06-01T00:00:00Z",
    "vigente_hasta": "2025-12-31T00:00:00Z"
  }
}
```

**Niveles de precio:**
1. `precio1_unidad` — Precio unitario al detal
2. `precio2_caja_bulto_pieza` — Precio por caja/bulto/pieza
3. `precio3_mayor_desde2` — Precio mayoreo desde 2 unidades
4. `precio4_mayorista` — Precio mayorista (opcional, puede requerir acuerdo)

---

### `GET /api/v1/precios/producto/{id}`
Lista precios de un producto (todas las sedes). **Público.**

---

### `GET /api/v1/precios/sede/{id}`
Lista precios de una sede (todos los productos). **Público.**

---

### `POST /api/v1/precios`
Crea un precio producto-sede.

**Auth:** Requiere permiso `price:create`

**Request:**
```json
{
  "producto_id": "uuid",
  "sede_id": "uuid",
  "precio1_unidad": 1.50,
  "precio2_caja_bulto_pieza": 1.20,
  "precio3_mayor_desde2": 1.00,
  "precio4_mayorista": 0.85,
  "precio4_requiere_acuerdo": false,
  "vigente_desde": "2025-06-01T00:00:00Z",
  "vigente_hasta": "2025-12-31T00:00:00Z"
}
```

Moneda se asigna automáticamente (`USD`).

---

### `PUT /api/v1/precios/{id}`
Actualiza un precio.

**Auth:** Requiere permiso `price:update`

---

### `DELETE /api/v1/precios/{id}`
Elimina un precio.

**Auth:** Requiere permiso `price:delete`

---

## 7. Sedes (`/api/v1/sedes`)

### `GET /api/v1/sedes`
Lista todas las sedes. **Público.**

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "id": "uuid",
      "nombre_tienda": "LCDPC Centro",
      "rif": "J-12345678-9",
      "direccion": "Av. Principal, Local 5",
      "telefono_contacto": "+582121234567",
      "telefono_contacto_secundario": null,
      "horario_atencion": "Lun-Vie 8am-6pm, Sab 8am-12pm",
      "created_at_utc": "2025-06-01T00:00:00Z",
      "updated_at_utc": "2025-06-01T00:00:00Z"
    }
  ]
}
```

---

### `POST /api/v1/sedes`
Crea una sede.

**Auth:** Requiere permiso `sede:create`

**Request:**
```json
{
  "nombre_tienda": "LCDPC Centro",
  "rif": "J-12345678-9",
  "direccion": "Av. Principal, Local 5",
  "telefono_contacto": "+582121234567",
  "telefono_contacto_secundario": "+584141234567",
  "horario_atencion": "Lun-Vie 8am-6pm, Sab 8am-12pm"
}
```

---

## 8. RBAC (`/api/v1/rbac`)

### Resources

#### `GET /api/v1/rbac/resources`
Lista todos los resources.

**Auth:** Requiere permiso `rbac:resource:view`

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {"id": "uuid", "code": "product:create"},
    {"id": "uuid", "code": "product:view"}
  ]
}
```

#### `GET /api/v1/rbac/resources/{id}`
Obtiene un resource por ID.

**Auth:** Requiere permiso `rbac:resource:view`

#### `POST /api/v1/rbac/resources`
Crea un resource.

**Auth:** Requiere permiso `rbac:resource:create`

**Request:**
```json
{"code": "product:create"}
```

#### `PUT /api/v1/rbac/resources/{id}`
Actualiza un resource.

**Auth:** Requiere permiso `rbac:resource:update`

**Request:**
```json
{"code": "product:create"}
```

#### `DELETE /api/v1/rbac/resources/{id}`
Elimina un resource.

**Auth:** Requiere permiso `rbac:resource:delete`

---

### Roles

#### `GET /api/v1/rbac/roles`
Lista todos los roles con sus resources asignados.

**Auth:** Requiere permiso `rbac:role:view`

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "id": "uuid",
      "code": "admin_global",
      "name": "admin_global",
      "description": "Administrador con alcance global",
      "resources": [
        {"id": "uuid", "code": "product:create"},
        {"id": "uuid", "code": "product:view"}
      ]
    }
  ]
}
```

#### `GET /api/v1/rbac/roles/{id}`
Obtiene un rol por ID con sus resources.

**Auth:** Requiere permiso `rbac:role:view`

#### `POST /api/v1/rbac/roles`
Crea un rol (sin resources aún).

**Auth:** Requiere permiso `rbac:role:create`

**Request:**
```json
{
  "code": "admin_sede",
  "name": "Administrador de Sede",
  "description": "Administrador con alcance a sedes asignadas"
}
```

#### `PUT /api/v1/rbac/roles/{id}`
Actualiza code, name, description de un rol.

**Auth:** Requiere permiso `rbac:role:update`

#### `DELETE /api/v1/rbac/roles/{id}`
Elimina un rol.

**Auth:** Requiere permiso `rbac:role:delete`

#### `POST /api/v1/rbac/roles/{id}/resources`
Asigna 1 resource al rol.

**Auth:** Requiere permiso `rbac:role:update`

**Request:**
```json
{"resource_id": "uuid"}
```

#### `DELETE /api/v1/rbac/roles/{id}/resources/{resourceId}`
Elimina 1 resource del rol.

**Auth:** Requiere permiso `rbac:role:update`

---

### Profiles

#### `GET /api/v1/rbac/profiles`
Lista todos los profiles con sus roles asignados.

**Auth:** Requiere permiso `rbac:profile:view`

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "id": "uuid",
      "user_id": "uuid",
      "first_name": "Juan",
      "last_name": "Pérez",
      "identity_document": "V-12345678",
      "rif": "J-12345678-9",
      "whatsapp_phone": "+584141234567",
      "full_address": "Calle 1, Edif 2, Apt 3",
      "roles": [
        {"id": "uuid", "code": "cliente", "name": "cliente"}
      ],
      "created_at_utc": "2025-06-01T00:00:00Z",
      "updated_at_utc": "2025-06-01T00:00:00Z"
    }
  ]
}
```

#### `GET /api/v1/rbac/profiles/{id}`
Obtiene un profile por ID con sus roles.

**Auth:** Requiere permiso `rbac:profile:view`

#### `POST /api/v1/rbac/profiles`
Crea un profile (sin roles aún).

**Auth:** Requiere permiso `rbac:profile:create`

**Request:**
```json
{
  "first_name": "Juan",
  "last_name": "Pérez",
  "identity_document": "V-12345678",
  "rif": "J-12345678-9",
  "whatsapp_phone": "+584141234567",
  "full_address": "Calle 1, Edif 2, Apt 3"
}
```

#### `PUT /api/v1/rbac/profiles/{id}`
Actualiza datos del profile.

**Auth:** Requiere permiso `rbac:profile:update`

#### `DELETE /api/v1/rbac/profiles/{id}`
Elimina un profile.

**Auth:** Requiere permiso `rbac:profile:delete`

#### `POST /api/v1/rbac/profiles/{id}/roles`
Asigna 1 role al profile.

**Auth:** Requiere permiso `rbac:profile:update`

**Request:**
```json
{"role_id": "uuid"}
```

#### `DELETE /api/v1/rbac/profiles/{id}/roles/{roleId}`
Elimina 1 role del profile.

**Auth:** Requiere permiso `rbac:profile:update`

---

### Users

#### `PUT /api/v1/users/{id}/profile`
Asigna o cambia el profile de un usuario.

**Auth:** Requiere permiso `rbac:user:update`

**Request:**
```json
{"profile_id": "uuid"}
```

---

## 9. Orders (`/api/v1/orders`)

### `POST /api/v1/orders`
Crea una orden con items.

**Auth:** Requiere permiso `order:create`

**Request:**
```json
{
  "sede_id": "uuid",
  "client_user_id": "uuid",
  "notes": "Entrega urgente",
  "items": [
    {
      "item_type": "producto",
      "producto_id": "uuid",
      "quantity": 5,
      "unit_price": 1.50
    },
    {
      "item_type": "combo",
      "combo_id": "uuid",
      "quantity": 2,
      "unit_price": 25.99
    }
  ]
}
```

**Validaciones:**
- Debe tener al menos 1 item
- `item_type` debe ser `producto` o `combo`
- Si `item_type` = `producto`: `producto_id` requerido, `combo_id` debe ser null
- Si `item_type` = `combo`: `combo_id` requerido, `producto_id` debe ser null

**Respuesta (201):** Orden completa con items y price_total calculado.

---

### `GET /api/v1/orders`
Lista órdenes con filtros opcionales.

**Auth:** Requiere permiso `order:view`

**Query params:**
| Param | Tipo | Descripción |
|-------|------|-------------|
| `sede_id` | UUID | Filtrar por sede |
| `client_user_id` | UUID | Filtrar por cliente |
| `status` | string | Filtrar por estado |

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "id": "uuid",
      "sede_id": "uuid",
      "client_user_id": "uuid",
      "status": "PENDING_REVIEW",
      "price_total": 59.48,
      "total_items": 7,
      "currency": "USD",
      "notes": "Entrega urgente",
      "created_at_utc": "2026-06-27T12:00:00Z",
      "updated_at_utc": "2026-06-27T12:00:00Z"
    }
  ]
}
```

---

### `GET /api/v1/orders/{id}`
Obtiene una orden por ID con sus items.

**Auth:** Requiere permiso `order:view`

**Respuesta:**
```json
{
  "status": "success",
  "data": {
    "id": "uuid",
    "sede_id": "uuid",
    "client_user_id": "uuid",
    "status": "PENDING_REVIEW",
    "price_total": 59.48,
    "total_items": 7,
    "currency": "USD",
    "notes": "Entrega urgente",
    "created_at_utc": "2026-06-27T12:00:00Z",
    "updated_at_utc": "2026-06-27T12:00:00Z",
    "items": [
      {
        "id": "uuid",
        "order_id": "uuid",
        "item_type": "producto",
        "producto_id": "uuid",
        "combo_id": null,
        "quantity": 5,
        "unit_price": 1.50,
        "subtotal": 7.50,
        "currency": "USD"
      }
    ]
  }
}
```

---

### `PUT /api/v1/orders/{id}`
Actualiza una orden (solo si está en estado editable: `PENDING_REVIEW` o `UNDER_REVIEW`).

**Auth:** Requiere permiso `order:update`

**Request:**
```json
{
  "notes": "Nota actualizada",
  "items": [
    {"item_type": "producto", "producto_id": "uuid", "quantity": 10, "unit_price": 1.50}
  ]
}
```

Si se proporciona `items`, se reemplazan todos los items existentes y se recalculan los totales.

**Errores:** `ORDER_NOT_EDITABLE`, `ORDER_NOT_FOUND`

---

### `DELETE /api/v1/orders/{id}`
Cancela una orden (cambia estado a `CANCELLED_BY_CUSTOMER`).

**Auth:** Requiere permiso `order:delete`

**Errores:** `ORDER_IN_TERMINAL_STATUS`, `ORDER_NOT_FOUND`

---

### `POST /api/v1/orders/{id}/status`
Cambia el estado de una orden.

**Auth:** Requiere permiso `order:status:change`

**Request:**
```json
{
  "to_status": "UNDER_REVIEW",
  "notes": "Revisando disponibilidad"
}
```

**Transiciones válidas:**
| Desde | Hacia |
|-------|-------|
| `PENDING_REVIEW` | `UNDER_REVIEW`, `REJECTED_BY_VALIDATION`, `CANCELLED_BY_CUSTOMER` |
| `UNDER_REVIEW` | `APPROVED_FOR_FULFILLMENT`, `REJECTED_BY_VALIDATION`, `CANCELLED_BY_CUSTOMER` |
| `APPROVED_FOR_FULFILLMENT` | `IN_PREPARATION`, `CANCELLED_BY_CUSTOMER` |
| `IN_PREPARATION` | `AWAITING_INVENTORY`, `PREPARATION_COMPLETED`, `CANCELLED_BY_CUSTOMER` |
| `AWAITING_INVENTORY` | `IN_PREPARATION`, `CANCELLED_BY_CUSTOMER` |
| `PREPARATION_COMPLETED` | `READY_FOR_PICKUP`, `READY_FOR_DISPATCH` |
| `READY_FOR_PICKUP` | `PICKED_UP`, `DELIVERY_FAILED` |
| `READY_FOR_DISPATCH` | `IN_TRANSIT`, `DELIVERY_FAILED` |
| `IN_TRANSIT` | `DELIVERED`, `DELIVERY_FAILED` |
| `DELIVERED` | `COMPLETED` |
| `PICKED_UP` | `COMPLETED` |

**Errores:** `INVALID_TRANSITION`, `ORDER_NOT_FOUND`

---

### `GET /api/v1/orders/{id}/history`
Obtiene el historial de cambios de estado de una orden.

**Auth:** Requiere permiso `order:view`

**Respuesta:**
```json
{
  "status": "success",
  "data": [
    {
      "id": "uuid",
      "order_id": "uuid",
      "from_status": null,
      "to_status": "PENDING_REVIEW",
      "changed_by_user_id": "uuid",
      "notes": null,
      "created_at_utc": "2026-06-27T12:00:00Z"
    },
    {
      "id": "uuid",
      "order_id": "uuid",
      "from_status": "PENDING_REVIEW",
      "to_status": "UNDER_REVIEW",
      "changed_by_user_id": "uuid",
      "notes": "Revisando disponibilidad",
      "created_at_utc": "2026-06-27T12:05:00Z"
    }
  ]
}
```

---

## 10. Sync (`/api/v1/sync`)

### `POST /api/v1/sync/productos`
Bulk upsert de productos (ON CONFLICT sku).

**Auth:** API Key en header `X-API-Key`

**Request:**
```json
[
  {
    "producto_id": "uuid",
    "nombre": "Coca-Cola 2L",
    "sku": "CC-2L",
    "tipo_medida_base": "Unidad",
    "tipo_comercial_mayor": "Caja",
    "unidades_por_caja": 6,
    "unidades_por_bulto": 24,
    "activo": true
  }
]
```

**Respuesta:**
```json
{"status": "success", "data": {"processed": 10, "errors": 0}}
```

---

### `POST /api/v1/sync/combos`
Bulk upsert de combos (ON CONFLICT codigo) + reemplazo de items.

**Auth:** API Key en header `X-API-Key`

**Request:**
```json
[
  {
    "combo_id": "uuid",
    "codigo": "COMBO-001",
    "nombre": "Combo Familiar",
    "estado": "Publicado",
    "sede_ids_habilitadas": ["uuid1"],
    "precio_total": 25.99,
    "precio_total_moneda": "USD",
    "precio_promocional": 19.99,
    "precio_promocional_moneda": "USD",
    "items": [
      {"producto_id": "uuid", "cantidad": 2}
    ]
  }
]
```

---

## 9. Autenticación — Resumen de Patrones

### Bearer Token (preferido)
```
Authorization: Bearer v2.local...
```

### Cookies (legacy fallback)
- `lcdpc_at` — Access token (HttpOnly, Lax, MaxAge = expires_in)
- `lcdpc_rt` — Refresh token (HttpOnly, Strict, MaxAge = 30 días)

### API Key (solo sync)
```
X-API-Key: {api-key-seed}
```

### Roles
| Rol | Permisos |
|-----|----------|
| `admin_global` | Todos los resources |
| `admin_sede` | CRUD productos, combos, precios, sedes, orders. Sin RBAC ni security-policy |
| `cliente` | Solo lectura: `product:view`, `combo:view`, `price:view`, `sede:view`, `order:create`, `order:view` |

---

## 10. Errores — Formato JSend

### `fail` (4xx — error del cliente)
```json
{
  "status": "fail",
  "data": {"email": "invalid"}
}
```

### `error` (4xx/5xx — error del servidor)
```json
{
  "status": "error",
  "message": "INVALID_CREDENTIALS"
}
```

### Códigos de error comunes
| Mensaje HTTP | Código interno | Significado |
|--------------|---------------|-------------|
| 400 | `INVALID_CREDENTIALS` | Email o password incorrecto |
| 400 | `EMAIL_ALREADY_REGISTERED` | Email ya registrado |
| 400 | `OTP_EXPIRED` | OTP venció |
| 400 | `OTP_ATTEMPTS_EXCEEDED` | Demasiados intentos OTP |
| 400 | `OTP_INVALID` | OTP incorrecto |
| 400 | `FLOW_NOT_FOUND` | Flow de registro no existe |
| 400 | `INVALID_REFRESH_TOKEN` | Refresh token inválido/expirado |
| 400 | `INVALID_OR_EXPIRED_RESET_TOKEN` | Token de reseteo inválido |
| 400 | `ORDER_NOT_FOUND` | Orden no existe |
| 400 | `ORDER_NOT_EDITABLE` | Orden no está en estado editable |
| 400 | `ORDER_IN_TERMINAL_STATUS` | Orden en estado terminal, no se puede cancelar |
| 400 | `INVALID_TRANSITION` | Transición de estado no permitida |
| 401 | `unauthorized` | Token no proporcionado o inválido |
| 403 | `insufficient_permissions` | Rol insuficiente |
| 404 | `NOT_FOUND` | Recurso no existe |
