# Plan: Endpoints de Sync con API Token

## Resumen

Endpoints de sincronización batch para productos y combos, autenticados con API tokens almacenados en DB. Upsert por SKU (productos) y Código (combos).

---

## 1. Nueva entidad `ApiToken` en Domain

**Archivo:** `LCDPC.Domain/Entities/Users/ApiToken.cs`

```csharp
public class ApiToken
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; }      // Nombre del consumidor
    public string TokenHash { get; private set; }    // SHA256 del token
    public bool Activo { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
```

- Factory method `Create()` con validación.
- Método `Desactivar()`.

## 2. Agregar campo `Codigo` a `Combo`

**Archivo:** `LCDPC.Domain/Entities/Pricing/Combo.cs`

- Agregar propiedad `Codigo` (string, único) para identificar combos externamente.
- Actualizar `Create()` para recibir `codigo`.
- Agregar método `ActualizarCodigo()`.

## 3. Filtro de autenticación por API Token

**Archivo:** `LCDPC.API/Security/ApiKeyAuthAttribute.cs`

- `TypeFilterAttribute` que valida header `X-API-Key`.
- Lee el token del header, busca hash en tabla `api_tokens`.
- Si no es válido o inactivo → 401.
- No interfiere con JWT (endpoints separados).

## 4. DTOs en Application

**Archivo:** `LCDPC.Application/Sync/Contracts.cs`

```csharp
// Productos
record SyncProductoRequest(string Sku, string Nombre, int TipoMedidaBase, int TipoComercialMayor, int? UnidadesPorCaja, int? UnidadesPorBulto);
record SyncProductoResponse(string Sku, string Accion); // "creado" | "actualizado"

// Combos
record SyncComboRequest(string Codigo, string Nombre, List<ComboItemRequest> Items, List<Guid>? SedeIdsHabilitadas);
record SyncComboResponse(string Codigo, string Accion);

// Batch
record SyncBatchResponse(int Creados, int Actualizados, int Errores, List<string> Detalles);

// Interface
interface ISyncService
{
    Task<SyncBatchResponse> SyncProductosAsync(List<SyncProductoRequest> productos, CancellationToken ct = default);
    Task<SyncBatchResponse> SyncCombosAsync(List<SyncComboRequest> combos, CancellationToken ct = default);
}
```

## 5. Lógica de sync en Infrastructure

**Archivo:** `LCDPC.Infrastructure/Sync/SyncService.cs`

- `SyncProductosAsync(List<SyncProductoRequest>)`:
  - Por cada producto: busca por SKU → si existe actualiza, si no crea.
  - Retorna resumen con counts.
- `SyncCombosAsync(List<SyncComboRequest>)`:
  - Por cada combo: busca por Código → si existe actualiza, si no crea.
  - Retorna resumen con counts.

## 6. Controllers

**Archivo:** `LCDPC.API/Controllers/SyncController.cs`

```
POST /api/v1/sync/productos  → [ApiKeyAuth] → batch upsert
POST /api/v1/sync/combos     → [ApiKeyAuth] → batch upsert
```

## 7. Configuración y DI

- `AppDbContext`: agregar `DbSet<ApiToken>` + configuración EF (tabla `api_tokens`).
- `DependencyInjection`: registrar `ISyncService`.

## 8. Seeder de API Token inicial

**Archivo:** `LCDPC.Infrastructure/Persistence/ApiTokenSeeder.cs`

- Genera un token inicial en el arranque (como `SuperUserSeeder`).
- El token en texto plano se loguea una sola vez al crear.

---

## Archivos a crear/modificar

| Acción | Archivo |
|--------|---------|
| **Crear** | `Domain/Entities/Users/ApiToken.cs` |
| **Modificar** | `Domain/Entities/Pricing/Combo.cs` (agregar `Codigo`) |
| **Crear** | `API/Security/ApiKeyAuthAttribute.cs` |
| **Crear** | `Application/Sync/Contracts.cs` |
| **Crear** | `Infrastructure/Sync/SyncService.cs` |
| **Crear** | `API/Controllers/SyncController.cs` |
| **Modificar** | `Infrastructure/Persistence/AppDbContext.cs` (DbSet + config) |
| **Modificar** | `Infrastructure/DependencyInjection.cs` (registrar servicio) |
| **Crear** | `Infrastructure/Persistence/ApiTokenSeeder.cs` |
| **Modificar** | `API/Program.cs` (ejecutar seeder) |

---

## Decisiones de diseño

- **Identificador de productos:** SKU (upsert por SKU).
- **Identificador de combos:** Campo nuevo `Codigo` (upsert por Código).
- **Formato de sync:** Array (batch) — múltiples productos/combos por llamada.
- **Modelo de API tokens:** Múltiples tokens en DB por consumidor, permite revocación individual.
- **Header de autenticación:** `X-API-Key`.
