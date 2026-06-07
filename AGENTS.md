# AGENTS

Project guidance for AI coding agents working in this repository.

## Scope

- This repo is a monorepo with backend in .NET and docs-first workflow.
- Main backend root: [api/LCDPC](api/LCDPC)
- Architecture and product context live in [docs](docs)

## Fast Start

- Prerequisites: .NET SDK 10, Docker/Compose (optional)
- Run backend locally:
  - `cd api/LCDPC`
  - `dotnet run --project LCDPC.API/LCDPC.API.csproj`
- Run with Docker:
  - `cd api/LCDPC`
  - `cp .env.example .env`
  - `docker compose up --build`
- Run tests:
  - `dotnet test api/LCDPC/LCDPC.slnx`

## Canonical Docs (Read Before Editing)

- Setup and repo overview: [README.md](README.md)
- Backend architecture blueprint: [docs/backend/architecture/fase-1-blueprint.md](docs/backend/architecture/fase-1-blueprint.md)
- SDD process overview: [docs/sdd/README.md](docs/sdd/README.md)
- Active SDD changes:
  - [docs/sdd/changes/usuarios-y-administradores/spec.md](docs/sdd/changes/usuarios-y-administradores/spec.md)
  - [docs/sdd/changes/usuarios-y-administradores/tasks.md](docs/sdd/changes/usuarios-y-administradores/tasks.md)
  - [docs/sdd/changes/pricing-sedes-y-mayor-detal/spec.md](docs/sdd/changes/pricing-sedes-y-mayor-detal/spec.md)
  - [docs/sdd/changes/pricing-sedes-y-mayor-detal/tasks.md](docs/sdd/changes/pricing-sedes-y-mayor-detal/tasks.md)

## Architecture Boundaries (Strict)

- Allowed dependencies:
  - `LCDPC.Application -> LCDPC.Domain`
  - `LCDPC.Infrastructure -> LCDPC.Application, LCDPC.Domain`
  - `LCDPC.API -> LCDPC.Application, LCDPC.Infrastructure, LCDPC.Domain`
- Forbidden:
  - `LCDPC.Domain` depending on any other project
  - `LCDPC.Application` depending on `LCDPC.Infrastructure` or `LCDPC.API`
  - Controllers directly depending on repositories

Reference: [docs/backend/architecture/fase-1-blueprint.md](docs/backend/architecture/fase-1-blueprint.md)

## Coding Conventions You Must Respect

- Keep business logic out of controllers.
- Package manager policy:
  - Use `pnpm` only for JavaScript/TypeScript package operations.
  - Do not use `npm` commands (install, run, exec, npx, global installs) unless the user explicitly approves in that conversation.
  - When docs or examples show `npm`, translate them to `pnpm` equivalents before executing.
- Use DI registrations through:
  - [api/LCDPC/LCDPC.Application/DependencyInjection.cs](api/LCDPC/LCDPC.Application/DependencyInjection.cs)
  - [api/LCDPC/LCDPC.Infrastructure/DependencyInjection.cs](api/LCDPC/LCDPC.Infrastructure/DependencyInjection.cs)
- Auth uses secure HTTP-only cookies and role filter:
  - [api/LCDPC/LCDPC.API/Controllers/AuthController.cs](api/LCDPC/LCDPC.API/Controllers/AuthController.cs)
  - [api/LCDPC/LCDPC.API/Security/RequireRolesAttribute.cs](api/LCDPC/LCDPC.API/Security/RequireRolesAttribute.cs)
  - [api/LCDPC/LCDPC.API/Security/RequireRolesFilter.cs](api/LCDPC/LCDPC.API/Security/RequireRolesFilter.cs)
- Domain response wrapper exists in:
  - [api/LCDPC/LCDPC.Domain/Common/JSendResponse.cs](api/LCDPC/LCDPC.Domain/Common/JSendResponse.cs)
  - Follow existing API style in target area; do not force broad response-shape rewrites unless requested.

## Persistence and Seed Notes

- EF Core context: [api/LCDPC/LCDPC.Infrastructure/Persistence/AppDbContext.cs](api/LCDPC/LCDPC.Infrastructure/Persistence/AppDbContext.cs)
- Superuser seeding: [api/LCDPC/LCDPC.Infrastructure/Persistence/SuperUserSeeder.cs](api/LCDPC/LCDPC.Infrastructure/Persistence/SuperUserSeeder.cs)
- Startup pipeline (db ensure + seed): [api/LCDPC/LCDPC.API/Program.cs](api/LCDPC/LCDPC.API/Program.cs)

## SDD Workflow Rule

- Before implementing a feature, read the matching spec/task docs in [docs/sdd/changes](docs/sdd/changes).
- After implementing, update the corresponding tasks file checklist.

## Practical Pitfalls

- Do not assume the frontend stack yet; [web](web) is currently scaffold-level.
- Be careful with cookie-based auth flows when adding endpoints (`lcdpc_at`, `lcdpc_rt`).
- Keep changes focused; avoid cross-layer refactors unless explicitly requested.
