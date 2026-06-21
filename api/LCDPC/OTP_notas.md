# Notas — Envío OTP y Migraciones

## Dominio de correo (Resend)

Actualmente el remitente está configurado como `onboarding@resend.dev` (dominio sandbox de Resend). Esto tiene las siguientes limitaciones:

- **Solo puede enviar correos a direcciones registradas en tu cuenta de Resend.** No funciona con cualquier destinatario hasta que verifiques tu propio dominio.
- Para producción, verificar tu dominio en el dashboard de Resend: https://resend.com/domains
- Una vez verificado, actualizar la variable `RESEND_FROM_ADDRESS` en `.env`:
  ```
  RESEND_FROM_ADDRESS=LCDPC <no-reply@tudominio.com>
  ```
  El valor por defecto en `docker-compose.yml` es `LCDPC <onboarding@resend.dev>` si no se define.

## Migraciones

- Se cambió `EnsureCreatedAsync()` por `MigrateAsync()` en `Program.cs`.
- Al levantar por primera vez con migraciones, se debe droppear el volumen existente:
  ```bash
  docker compose down -v
  docker compose up --build
  ```
- Las 13 migraciones existentes en `LCDPC.Infrastructure/Persistence/Migrations/` se aplicarán automáticamente al arrancar el contenedor.

## API Key de Resend

- La API key se almacena en `.env` (que está en `.gitignore`).
- Se pasa al contenedor vía `docker-compose.yml` como variable de entorno `RESEND_APITOKEN`.
- En `Program.cs` se lee con `Environment.GetEnvironmentVariable("RESEND_APITOKEN")`.

## Flujo OTP

1. `POST /api/v1/auth/register/start` → genera OTP de 6 dígitos, guarda en BD, envía correo con Resend.
2. `POST /api/v1/auth/register/verify-email` → valida OTP (2 min TTL, 5 intentos máx).
3. `POST /api/v1/auth/register/profile` → completa registro.

## Flujo Forgot Password

1. `POST /api/v1/auth/forgot-password` → genera token, guarda en BD, envía correo con Resend. El token ya no se expone en la respuesta HTTP.
2. `POST /api/v1/auth/reset-password` → valida token y cambia contraseña.
