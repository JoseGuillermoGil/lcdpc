# LCDPC API — Blueprint Técnico Fase 1

## 1) Objetivo de Fase 1

Diseñar la base técnica de la nueva solución `LCDPC` en Clean Architecture, **sin implementación funcional completa**, dejando listo:

- alcance v1 congelado
- estructura de solución y proyectos
- reglas de dependencia
- convenciones de carpetas/namespaces
- contratos internos (Application)
- checklist de aceptación para iniciar Fase 2

---

## 2) Alcance v1 (congelado)

### 2.1 Módulos funcionales v1

1. **Usuarios y Administradores**
   - registro cliente por pasos (OTP correo -> perfil mínimo -> confirmación)
   - login cliente/admin
   - Google OAuth2/OIDC para iniciar registro y prefill paso 2
   - recuperación cliente por correo afiliado
   - reset admin vía `admin_global` (módulo refrescamiento)

2. **Seguridad de autenticación**
   - política OTP fija v1: `5 intentos`, `TTL 10 min`, `cooldown 10 min`
   - política reset token configurable por dashboard (TTL + revocación sesiones)

3. **Comercial base**
   - pricing por sede
   - carrito de sede única

### 2.2 Fuera de alcance en Fase 1

- integración real con pasarela de pagos
- optimizaciones de performance avanzadas
- hardening profundo de infraestructura (WAF, SIEM productivo)
- features no incluidas en SDD actual

---

## 3) Arquitectura objetivo

La solución seguirá estos proyectos:

- `LCDPC.Domain`
- `LCDPC.Application`
- `LCDPC.Infrastructure`
- `LCDPC.API`
- `LCDPC.Architecture.Tests`

### 3.1 Regla de oro

La lógica de negocio nunca depende de frameworks o infraestructura.

---

## 4) Reglas de dependencia (obligatorias)

## Permitido

- `LCDPC.Application` -> `LCDPC.Domain`
- `LCDPC.Infrastructure` -> `LCDPC.Application`, `LCDPC.Domain`
- `LCDPC.API` -> `LCDPC.Application`, `LCDPC.Infrastructure`, `LCDPC.Domain`
- `LCDPC.Architecture.Tests` -> todos (solo para validación)

## Prohibido

- `LCDPC.Domain` -> cualquier otro proyecto
- `LCDPC.Application` -> `LCDPC.Infrastructure` o `LCDPC.API`
- `Controllers` -> `Repositories` directamente

---

## 5) Estructura propuesta de carpetas

## 5.1 `LCDPC.Domain`

- `Common/`
  - `JSendResponse.cs`
  - `ErrorCodes.cs`
- `Enums/`
- `ValueObjects/`
- `Entities/`
  - `Users/`
  - `Pricing/`
  - `Cart/`
- `Events/`
- `Rules/`

## 5.2 `LCDPC.Application`

- `Abstractions/`
  - `Persistence/` (repositorios)
  - `Security/`
  - `Messaging/`
- `UseCases/`
  - `Users/`
    - `RegisterStart/`
    - `RegisterVerifyOtp/`
    - `RegisterProfile/`
    - `Login/`
    - `ForgotPassword/`
    - `ResetPassword/`
  - `Admin/`
    - `ForceResetAdmin/`
    - `UpdateAuthPolicy/`
  - `Pricing/`
  - `Cart/`
- `DTOs/`
- `Services/`
- `DependencyInjection.cs`

## 5.3 `LCDPC.Infrastructure`

- `Persistence/`
  - `AppDbContext.cs`
  - `Configurations/`
  - `Migrations/`
- `Repositories/`
- `Security/`
  - `Jwt/`
  - `Google/`
  - `Otp/`
- `Email/`
- `Audit/`
- `Transactions/`
- `DependencyInjection.cs`

## 5.4 `LCDPC.API`

- `Controllers/`
  - `AuthController.cs`
  - `AdminUsersController.cs`
  - `AdminSecurityController.cs`
  - `PricingController.cs`
  - `CartController.cs`
- `Middleware/`
  - `ExceptionHandlerMiddleware.cs`
  - `AuditMiddleware.cs`
  - `RequestLoggingMiddleware.cs`
- `Extensions/`
- `Contracts/` (request/response HTTP)
- `Program.cs`
- `appsettings*.json`

## 5.5 `LCDPC.Architecture.Tests`

- `DependencyRulesTests.cs`
- `ControllerRulesTests.cs`
- `DiRegistrationTests.cs`
- `NamingConventionsTests.cs`

---

## 6) Convenciones técnicas

## 6.1 Nombres y namespaces

- Root namespace: `LCDPC`
- Proyecto API: `LCDPC.API`
- Proyecto Domain: `LCDPC.Domain`
- Proyecto Application: `LCDPC.Application`
- Proyecto Infrastructure: `LCDPC.Infrastructure`

## 6.2 Estándar de respuestas

Toda respuesta de negocio y error controlado usa `JSendResponse`.

## 6.3 Dependencias externas

Las dependencias de framework viven en `Infrastructure` y `API`, no en `Domain`.

---

## 7) Diseño base de módulos (sin implementación)

## 7.1 Usuarios/Auth

- flujo de onboarding por estado (`pendiente_verificacion`, `pendiente_perfil`, `activo`)
- OTP con política v1 fija
- recovery por correo afiliado para clientes
- recovery admins por acción de `admin_global`
- prefill Google con claims OIDC:
  - `email`
  - `email_verified`
  - `name`
  - `given_name`
  - `family_name`
  - `picture`
  - `locale`

## 7.2 Pricing y carrito

- cálculo por sede activa
- carrito no mezcla sedes
- niveles de precio:
  - nivel 1: unidad/peso
  - nivel 2: 1 caja/bulto/pieza
  - nivel 3: >1 y <=50 caja/bulto/pieza
  - nivel 4: >50 caja/bulto/pieza

---

## 8) Contratos internos mínimos (Application)

Interfaces mínimas (diseño):

- `IUserRepository`
- `IAdminUserRepository`
- `IAuthPolicyRepository`
- `IOtpRepository`
- `IPasswordResetRepository`
- `IPricingRepository`
- `ICartRepository`
- `ITransactionManager`
- `IEmailSender`
- `IGoogleTokenValidator`
- `IJwtTokenService`
- `IAuditWriter`

---

## 9) Observabilidad base (definición)

- logging estructurado por request
- correlación por `TraceId`
- auditoría obligatoria en mutaciones admin
- middleware global de excepciones con mapeo de `ErrorCodes`
- health endpoints:
  - `/health`
  - `/health/ready`

---

## 10) Criterios de aceptación de Fase 1

Se considera Fase 1 completada cuando:

1. existe diseño final aprobado de la solución `LCDPC` y sus 5 proyectos
2. reglas de dependencia están documentadas y validadas en tests de arquitectura
3. alcance v1 está congelado y fuera de alcance explícito
4. estructura de carpetas y convenciones están definidas
5. módulos críticos (`Users/Auth`, `Pricing`, `Cart`) tienen blueprint interno y contratos base
6. checklist de entrada a Fase 2 está firmado por equipo

---

## 11) Riesgos y mitigación

1. **Riesgo**: arrastrar patrones del template anterior.
   - **Mitigación**: nueva solución `LCDPC` en paralelo; cero renombre incremental.

2. **Riesgo**: ambigüedad en contratos API.
   - **Mitigación**: contratos versionados antes de codificar use cases.

3. **Riesgo**: acoplamiento Controller -> Repository.
   - **Mitigación**: regla de arquitectura + tests automáticos.

4. **Riesgo**: inconsistencias OTP/recovery.
   - **Mitigación**: una sola fuente de verdad: `AuthPolicy` + pruebas específicas.

---

## 12) Entregables de Fase 1

- `docs/architecture/fase-1-blueprint.md` (este documento)
- `docs/sdd/changes/*` validados y alineados
- checklist de go/no-go a Fase 2

---

## 13) Go / No-Go para Fase 2

**GO** si:

- alcance y reglas cerradas
- contratos cerrados
- arquitectura aprobada

**NO-GO** si:

- existen decisiones pendientes en seguridad/auth
- existen conflictos en políticas de recovery/roles
- no hay consenso en estructura de solución
