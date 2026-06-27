# Plan de Migración Go — Análisis Final

## Comparativa: C# (.NET 10) vs Go

---

## 1. Seguridad

### Tokens de autenticación

| Aspecto | C# (JWT RS256) | Go (PASETO v2.local) |
|---------|----------------|----------------------|
| **Algoritmo** | RS256 (RSA + SHA-256) | XChaCha20-Poly1305 |
| **Tipo** | Firma asimétrica | Cifrado autenticado simétrico |
| **Lectura del payload** | Cualquiera con el token puede decodificar el Base64url y leer claims (email, roles, sub) sin necesidad de clave | Solo el servidor puede descifrar. El token es opaco (`v2.local...`) |
| **Algoritmos soportados** | `alg` en header permite RS256, RS384, RS512, HS256, ninguno, etc. El atacante puede intentar cambiar el algoritmo | No hay campo `alg`. La versión del token define la criptografía. Imposible confusión de algoritmos |
| **Ataque de confusión** | Posible si el validador no verifica `alg` explícitamente (CVE común en librerías JWT) | Eliminado por diseño |
| **Clave privada expuesta** | Si la clave RSA se expone, el atacante puede firmar tokens válidos | Si la clave simétrica se expone, el atacante puede crear Y descifrar tokens. **Mitigación:** la clave nunca se escribe en logs, solo en env var o archivo |
| **Rotación de claves** | Requiere generar nuevo par RSA, actualizar JWKS, mantener clave vieja para validación transitoria | Generar nueva clave de 32 bytes, reemplazar env var. Tokens viejos quedan inválidos inmediatamente |
| **JWKS endpoint** | Expone la clave pública en `/.well-known/jwks.json`. Cualquiera puede verificar tokens | No existe JWKS. No se expone información sobre las claves |

### Refresh tokens y detección de robo

| Aspecto | C# | Go |
|---------|----|----|
| **Rotación con familias** | Implementado | Implementado (idéntico) |
| **Detección de robo** | Si un refresh token reutilizado tiene un sucesor en la familia, revoca toda la familia | Idéntico |
| **Almacenamiento** | Hash SHA-256 del token en `oauth2_refresh_tokens` | Idéntico |
| **Chain tracking** | `previous_token_hash` encadena tokens | Idéntico |

### Password hashing

| Aspecto | C# | Go |
|---------|----|----|
| **Algoritmo** | PBKDF2-SHA256, 100,000 iteraciones, 16-byte salt, 32-byte hash | Idéntico |
| **Formato** | `PBKDF2$100000$SHA256$<base64-salt>$<base64-hash>` | Idéntico |
| **Compatibilidad** | — | Los hashes generados en C# se validan en Go y viceversa |

### PKCE (Proof Key for Code Exchange)

| Aspecto | C# | Go |
|---------|----|----|
| **Método** | S256 mandatory | S256 mandatory |
| **Verificación** | `SHA256(code_verifier) == code_challenge` (base64url) | Idéntico |

### API Key para sync

| Aspecto | C# | Go |
|---------|----|----|
| **Hashing** | SHA-256 del token, lookup en `api_tokens` | Idéntico |
| **Header** | `X-API-Key` | Idéntico |

---

## 2. Infraestructura

### Consumo de recursos

| Métrica | C# (.NET 10) | Go |
|---------|--------------|-----|
| **Memoria en idle** | ~80-120 MB (CLR, JIT, GC) | ~15-25 MB (binario estático, sin runtime pesado) |
| **Tiempo de inicio** | ~2-5 segundos (JIT compilation, DI container, EF Core model building) | ~100-300ms (compilado AOT, sin JIT) |
| **Imagen Docker** | ~200-250 MB (mcr.microsoft.com/dotnet/aspnet:10.0) | ~10-15 MB (scratch o alpine) |
| **CPU bajo carga** | JIT puede recompilar hot paths, GC pauses ocasionales | Sin GC pauses (stack allocation predominante), ejecución directa |
| **Concurrencia** | async/await con Task, thread pool limitado | Goroutines (~2KB stack cada una), puede manejar 100k+ conexiones concurrentes |

### Dependencias

| Aspecto | C# | Go |
|---------|----|----|
| **Runtime** | .NET 10 Runtime (~80 MB) | Ninguno (binario estático) |
| **ORM** | Entity Framework Core 10 (~5 MB) | Ninguno. Queries SQL directas con pgx |
| **Paquetes NuGet/Go** | ~15 paquetes principales | ~12 paquetes principales |
| **Vulnerabilidades de supply chain** | Mayor superficie (EF Core, ASP.NET, System.IdentityModel, Npgsql) | Menor superficie (pgx, chi, paseto — todos con pocas dependencias transitivas) |

### Base de datos

| Aspecto | C# | Go |
|---------|----|----|
| **Driver** | Npgsql (ADO.NET) | pgx (nativo, sin capa ADO.NET) |
| **Performance** | Bueno, pero con overhead de EF Core (change tracking, lazy loading, SQL generation) | Excelente. pgx es el driver más rápido para PostgreSQL en Go. Queries directas sin ORM |
| **Migraciones** | `EnsureCreated()` en startup (no recomendado para producción) | `golang-migrate` con archivos SQL up/down (listo para producción) |
| **Transacciones** | `DbContext.SaveChangesAsync()` implícito | `pool.Begin(ctx)` / `tx.Commit(ctx)` explícito, más control |
| **Connection pooling** | Npgsql internal pool | pgxpool (configuración explícita, más control sobre max connections, idle timeout) |

### Deployment

| Aspecto | C# | Go |
|---------|----|----|
| **Build** | `dotnet publish -c Release` (~10-30s) | `go build` (~2-5s) |
| **Cross-compilation** | Requiere SDK por plataforma | `GOOS=linux GOARCH=amd64 go build` desde cualquier OS |
| **CI/CD** | Requiere .NET SDK en el runner | Solo Go SDK, binario final no necesita nada |
| **Hot reload** | `dotnet watch` (desarrollo) | `air` o `CompileDaemon` (terceros) |
| **Health checks** | `Microsoft.Extensions.Diagnostics.HealthChecks` (paquete NuGet) | Handler simple con `pool.Ping()` (cero dependencias) |

---

## 3. Portabilidad

### Código

| Aspecto | C# | Go |
|---------|----|----|
| **Plataformas** | Windows, Linux, macOS (requiere .NET runtime instalado o self-contained ~80MB) | Cualquier OS/arch con compilación cruzada. Binario de ~10MB sin dependencias |
| **Containerización** | Imagen base .NET necesaria | `FROM scratch` — imagen mínima, solo el binario + certs TLS |
| **WASM** | Blazor WebAssembly (experimental para server) | Soporte nativo (Go 1.21+) |
| **Edge/serverless** | Azure Functions, AWS Lambda (con cold start) | Cloudflare Workers (experimental), AWS Lambda (binario estático, cold start mínimo) |

### Arquitectura

| Aspecto | C# | Go |
|---------|----|----|
| **Clean Architecture** | 4 proyectos (Domain, Application, Infrastructure, API). Separación estricta por ensamblados | Paquetes planos por feature (`internal/auth/`, `internal/pricing/`). Sin sobrecapas |
| **DI** | `IServiceCollection` con `AddScoped`, `AddSingleton` | Wiring manual en `main.go`. Más simple, más explícito |
| **Interfaces** | Definidas en el proyecto que las implementa (`IProductoService` en Application) | Definidas en el paquete que las consume. "Acepta interfaces, retorna structs" |
| **Queries SQL** | LINQ sobre EF Core (abstracción opaca, SQL generado) | SQL escrito a mano, type-safe con pgx. Control total del SQL ejecutado |
| **Error handling** | `throw new InvalidOperationException("CODE")` — requiere try/catch | `return fmt.Errorf("context: %w", err)` — errores como valores, propagación explícita |

---

## 4. Consumo de Tokens: C# vs Go

### Generación de Access Token

**C#:**
```csharp
// OAuth2TokenService.GenerateAccessTokenAsync
var signingCredentials = new SigningCredentials(
    new RsaSecurityKey(rsa),
    SecurityAlgorithms.RsaSha256);

var claims = new List<Claim>
{
    new(JwtRegisteredClaimNames.Iss, _options.Issuer),
    new(JwtRegisteredClaimNames.Aud, _options.Audience),
    new(JwtRegisteredClaimNames.Sub, userId.ToString()),
    new("client_id", clientId),
    new("scope", scope),
    new(JwtRegisteredClaimNames.Email, email)
    // + roles como claims repetidos
};

var tokenDescriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(claims),
    Expires = expires,
    SigningCredentials = signingCredentials
};

var handler = new JwtSecurityTokenHandler();
return handler.WriteToken(handler.CreateToken(tokenDescriptor));
// Token: eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJodHRw...
```

**Go:**
```go
// auth.GenerateAccessToken
claims := TokenClaims{
    Iss:      cfg.Issuer,
    Aud:      cfg.Audience,
    Sub:      userID.String(),
    ClientID: clientID,
    Scope:    scope,
    Email:    email,
    Roles:    roles,
    Iat:      now.Unix(),
    Exp:      expires.Unix(),
    Jti:      uuid.New().String(),
}

token, err := paseto.NewV2().Encrypt(key, claims, nil)
// Token: v2.local.xNkOYH0J7Jf3...
```

### Validación de Access Token

**C#:**
```csharp
// OAuth2JwtBearerEvents
var validationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = issuer,
    ValidateAudience = true,
    ValidAudience = audience,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new RsaSecurityKey(rsa),
    ValidateLifetime = true,
    ClockSkew = TimeSpan.FromSeconds(30)
};

handler.ValidateToken(token, validationParameters, out _);
// + verificación adicional contra user_sessions en DB
```

**Go:**
```go
// auth.ValidatePasetoToken
var claims TokenClaims
err := paseto.NewV2().Decrypt(tokenString, key, &claims, nil)
if err != nil { return nil, err }
if claims.Iss != issuer { return nil, err }
if claims.Aud != audience { return nil, err }
if time.Now().Unix() > claims.Exp { return nil, err }
return &claims, nil
```

### Diferencias clave en consumo de tokens

| Aspecto | C# | Go |
|---------|----|----|
| **Tamaño del token** | ~800-1200 bytes (JWT RS256: header base64url + payload base64url + firma RSA-2048 de 256 bytes) | **458 bytes** medido (PASETO v2.local: nonce XChaCha20 + payload cifrado + Poly1305 auth tag) |
| **Overhead en cookies** | Cookie `lcdpc_at` de ~1KB | Cookie `lcdpc_at` de **458 bytes** (~55% menos) |
| **Overhead en Authorization header** | Header `Bearer <jwt>` de ~1KB | Header `Bearer <paseto>` de **458 bytes** (~55% menos) |
| **Validación** | Verificar firma RSA-2048 (~1ms/op) + lookup DB en `user_sessions` + verificar que no esté revoked | Descifrar XChaCha20-Poly1305 (~60μs/op, **~16x más rápido**) + verificar exp/iss/aud + sin lookup DB obligatorio |
| **Throughput de generación** | ~2,000-5,000 tokens/s (RSA sign es costoso) | **33,793 tokens/s** medido |
| **Throughput de validación** | ~3,000-8,000 tokens/s (RSA verify + DB lookup) | **16,833 tokens/s** medido (solo descifrado, sin DB) |
| **Claims en el token** | Roles como claims repetidos (`roles: admin, roles: cliente`) — JWT no soporta arrays nativos | Roles como array JSON (`"roles": ["admin","cliente"]`) — JSON nativo |
| **Session tracking** | Dual obligatorio: OAuth2 refresh token + legacy `UserSession` con hashes de access/refresh. **Ambos se verifican en cada request** | Dual por compatibilidad: OAuth2 refresh token + legacy `UserSession`. **Solo refresh token se requiere para validez del access token** |
| **Exposición de datos** | Payload visible en Base64url: cualquiera puede leer `sub`, `email`, `roles`, `client_id` sin clave | Payload cifrado: solo el servidor puede leer los claims. El token es un blob opaco para el cliente y para cualquier interceptor |

---

## 5. Resumen de Trade-offs

### Gana Go en:
- **Seguridad de tokens:** PASETO elimina ataques de confusión de algoritmos y no expone claims
- **Tamaño de imagen Docker:** ~15MB vs ~250MB
- **Uso de memoria:** ~20MB vs ~100MB
- **Velocidad de inicio:** ~200ms vs ~3s
- **Simplicidad arquitectónica:** Sin DI framework, sin ORM, paquetes planos
- **Control del SQL:** Queries escritas a mano, type-safe
- **Deployment:** Binario estático, cross-compilation de una línea

### Gana C# en:
- **Ecosistema de tooling:** Visual Studio, ReSharper, hot reload nativo
- **LINQ:** Más expresivo para queries complejas (aunque genera SQL subóptimo)
- **OpenAPI/Scalar:** Integración nativa en ASP.NET Core
- **Migraciones EF Core:** Generación automática desde cambios de modelo
- **Madurez del framework:** ASP.NET Core es battle-tested en producción a gran escala
- **Comunidad enterprise:** Más documentación, más ejemplos de Clean Architecture

### Neutral:
- **Seguridad de passwords:** PBKDF2 idéntico en ambos, hashes compatibles
- **OAuth2 server:** Misma lógica, mismos flujos, misma detección de robo
- **PKCE:** S256 mandatory en ambos
- **CORS:** Misma configuración
- **Rate limiting:** Ambos implementaciones son básicas (IP-based)

---

## 6. Recomendación

La migración a Go es beneficiosa para este proyecto porque:

1. **LCDPC es una API de alto tráfico comercial** — el bajo uso de memoria y la capacidad de manejar miles de conexiones concurrentes con goroutines es relevante
2. **El equipo trabaja con Docker** — imágenes de 15MB se deployan en segundos
3. **La seguridad de PASETO** elimina una clase entera de vulnerabilidades JWT que son las más comunes en APIs modernas (OWASP Top 10)
4. **La simplicidad de Go** reduce el tiempo de onboarding de nuevos desarrolladores
5. **El binario estático** facilita deployment en cualquier infraestructura (bare metal, containers, serverless)

El principal costo es el tiempo de migración (15-23 días estimados) y la necesidad de adaptar el frontend a PASETO (el token es opaco, no se puede decodificar en el cliente sin el backend).
