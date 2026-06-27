using LCDPC.Domain.Entities.OAuth2;
using LCDPC.Domain.Entities.Pricing;
using LCDPC.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace LCDPC.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private static readonly Guid AuthSecurityPolicyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ApiResource> ApiResources => Set<ApiResource>();
    public DbSet<RoleResourcePermission> RoleResourcePermissions => Set<RoleResourcePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Sede> Sedes => Set<Sede>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuthSecurityPolicy> AuthSecurityPolicies => Set<AuthSecurityPolicy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RegistrationFlow> RegistrationFlows => Set<RegistrationFlow>();
    public DbSet<OAuth2Client> OAuth2Clients => Set<OAuth2Client>();
    public DbSet<OAuth2AuthorizationCode> OAuth2AuthorizationCodes => Set<OAuth2AuthorizationCode>();
    public DbSet<OAuth2RefreshToken> OAuth2RefreshTokens => Set<OAuth2RefreshToken>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<PrecioProductoSede> PreciosProductoSede => Set<PrecioProductoSede>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    private static readonly Guid ClientRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminSedeRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminGlobalRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ResourceAuthRegisterStartId = Guid.Parse("44444444-4444-4444-4444-444444444441");
    private static readonly Guid ResourceHealthApiId = Guid.Parse("44444444-4444-4444-4444-444444444442");
    private static readonly Guid ResourceHealthProbeId = Guid.Parse("44444444-4444-4444-4444-444444444443");
    private static readonly DateTime SeedTimestamp = new(2026, 5, 16, 0, 0, 0, DateTimeKind.Utc);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.OnboardingStatus).HasMaxLength(40).IsRequired();
            entity.Property(x => x.EmailVerifiedAtUtc).IsRequired(false);
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();

            entity.HasOne(x => x.Profile)
                .WithOne(x => x.User)
                .HasForeignKey<Profile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.RoleAssignments)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Sessions)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.PasswordResetTokens)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.ToTable("profiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IdentityDocument).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Rif).HasMaxLength(40);
            entity.Property(x => x.WhatsAppPhone).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FullAddress).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.IdentityDocument).IsUnique();
            entity.HasIndex(x => x.Rif).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();

            entity.HasMany(x => x.ResourcePermissions)
                .WithOne(x => x.Role)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new Role
                {
                    Id = ClientRoleId,
                    Code = "cliente",
                    Name = "cliente",
                    Description = "Rol cliente para operaciones comerciales"
                },
                new Role
                {
                    Id = AdminSedeRoleId,
                    Code = "admin_sede",
                    Name = "admin_sede",
                    Description = "Administrador con alcance a sedes asignadas"
                },
                new Role
                {
                    Id = AdminGlobalRoleId,
                    Code = "admin_global",
                    Name = "admin_global",
                    Description = "Administrador con alcance global"
                });
        });

        modelBuilder.Entity<ApiResource>(entity =>
        {
            entity.ToTable("api_resources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Endpoint).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.Endpoint, x.Method }).IsUnique();

            entity.HasData(
                new ApiResource
                {
                    Id = ResourceAuthRegisterStartId,
                    Code = "auth.register.start",
                    Name = "Iniciar registro de cliente",
                    Endpoint = "/api/v1/auth/register/start",
                    Method = "POST",
                    Description = "Endpoint para iniciar flujo de registro y emitir OTP"
                },
                new ApiResource
                {
                    Id = ResourceHealthApiId,
                    Code = "health.api.get",
                    Name = "Health API Controller",
                    Endpoint = "/api/health",
                    Method = "GET",
                    Description = "Health endpoint del controlador API"
                },
                new ApiResource
                {
                    Id = ResourceHealthProbeId,
                    Code = "health.probe.get",
                    Name = "Health probe",
                    Endpoint = "/health",
                    Method = "GET",
                    Description = "Health check de infraestructura"
                });
        });

        modelBuilder.Entity<RoleResourcePermission>(entity =>
        {
            entity.ToTable("role_resource_permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RoleId).IsRequired();
            entity.Property(x => x.ResourceId).IsRequired();
            entity.Property(x => x.CanView).IsRequired();
            entity.Property(x => x.CanWrite).IsRequired();
            entity.Property(x => x.CanUpdate).IsRequired();
            entity.Property(x => x.CanDelete).IsRequired();
            entity.Property(x => x.CanAll).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.RoleId, x.ResourceId }).IsUnique();

            entity.HasOne(x => x.Resource)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555551"),
                    RoleId = ClientRoleId,
                    ResourceId = ResourceAuthRegisterStartId,
                    CanView = false,
                    CanWrite = true,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555552"),
                    RoleId = AdminSedeRoleId,
                    ResourceId = ResourceAuthRegisterStartId,
                    CanView = false,
                    CanWrite = true,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555553"),
                    RoleId = AdminGlobalRoleId,
                    ResourceId = ResourceAuthRegisterStartId,
                    CanView = false,
                    CanWrite = true,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555554"),
                    RoleId = ClientRoleId,
                    ResourceId = ResourceHealthApiId,
                    CanView = true,
                    CanWrite = false,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    RoleId = AdminSedeRoleId,
                    ResourceId = ResourceHealthApiId,
                    CanView = true,
                    CanWrite = false,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555556"),
                    RoleId = AdminGlobalRoleId,
                    ResourceId = ResourceHealthApiId,
                    CanView = true,
                    CanWrite = false,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555557"),
                    RoleId = ClientRoleId,
                    ResourceId = ResourceHealthProbeId,
                    CanView = true,
                    CanWrite = false,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555558"),
                    RoleId = AdminSedeRoleId,
                    ResourceId = ResourceHealthProbeId,
                    CanView = true,
                    CanWrite = false,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                },
                new RoleResourcePermission
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555559"),
                    RoleId = AdminGlobalRoleId,
                    ResourceId = ResourceHealthProbeId,
                    CanView = true,
                    CanWrite = false,
                    CanUpdate = false,
                    CanDelete = false,
                    CanAll = false,
                    CreatedAtUtc = SeedTimestamp
                });
        });

        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.ToTable("user_role_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.RoleId).IsRequired();
            entity.Property(x => x.SedeIds)
                .HasColumnType("uuid[]")
                .IsRequired();
            entity.Property(x => x.Active).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.UserId, x.RoleId, x.Active });

            entity.HasOne(x => x.Role)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.AccessTokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RefreshTokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.AccessTokenExpiresAtUtc).IsRequired();
            entity.Property(x => x.RefreshTokenExpiresAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.RevokedAtUtc).IsRequired(false);

            entity.HasIndex(x => x.AccessTokenHash).IsUnique();
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
        });

        modelBuilder.Entity<Sede>(entity =>
        {
            entity.ToTable("sedes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NombreTienda).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Rif).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Direccion).HasMaxLength(500).IsRequired();
            entity.Property(x => x.TelefonoContacto).HasMaxLength(30).IsRequired();
            entity.Property(x => x.TelefonoContactoSecundario).HasMaxLength(30).IsRequired(false);
            entity.Property(x => x.HorarioAtencion).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasIndex(x => x.Rif).IsUnique();
            entity.HasIndex(x => x.NombreTienda);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UsedAtUtc).IsRequired(false);
            entity.Property(x => x.RevokedAtUtc).IsRequired(false);

            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc, x.UsedAtUtc });
        });

        modelBuilder.Entity<AuthSecurityPolicy>(entity =>
        {
            entity.ToTable("auth_security_policies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PasswordResetTtlMinutes).IsRequired();
            entity.Property(x => x.RevokeSessionsOnPasswordReset).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();

            entity.HasData(new AuthSecurityPolicy
            {
                Id = AuthSecurityPolicyId,
                PasswordResetTtlMinutes = 30,
                RevokeSessionsOnPasswordReset = true,
                UpdatedAtUtc = SeedTimestamp
            });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired(false);
            entity.Property(x => x.ActionCode).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Area).HasMaxLength(80).IsRequired();
            entity.Property(x => x.MetadataJson).HasMaxLength(4000).IsRequired(false);
            entity.Property(x => x.IpAddress).HasMaxLength(80).IsRequired(false);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.Area, x.ActionCode });
        });

        modelBuilder.Entity<RegistrationFlow>(entity =>
        {
            entity.ToTable("registration_flows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OtpCode).HasMaxLength(6).IsRequired();
            entity.Property(x => x.OtpHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.VerifiedAtUtc).IsRequired(false);
            entity.Property(x => x.OtpExpiresAtUtc).IsRequired();
            entity.Property(x => x.OtpAttempts).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<OAuth2Client>(entity =>
        {
            entity.ToTable("oauth2_clients");
            entity.HasKey(x => x.ClientId);
            entity.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RedirectUris)
                .HasColumnType("jsonb")
                .IsRequired();
            entity.Property(x => x.GrantTypes)
                .HasColumnType("jsonb")
                .IsRequired();
            entity.Property(x => x.RequirePkce).IsRequired();
            entity.Property(x => x.AllowedScopes).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<OAuth2AuthorizationCode>(entity =>
        {
            entity.ToTable("oauth2_authorization_codes");
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.RedirectUri).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Scope).HasMaxLength(500).IsRequired();
            entity.Property(x => x.CodeChallenge).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CodeChallengeMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.UsedAtUtc).IsRequired(false);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => x.ClientId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ExpiresAtUtc);
        });

        modelBuilder.Entity<OAuth2RefreshToken>(entity =>
        {
            entity.ToTable("oauth2_refresh_tokens");
            entity.HasKey(x => x.TokenHash);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ClientId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.Scope).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FamilyId).IsRequired();
            entity.Property(x => x.PreviousTokenHash).HasMaxLength(128).IsRequired(false);
            entity.Property(x => x.ExpiresAtUtc).IsRequired();
            entity.Property(x => x.RevokedAtUtc).IsRequired(false);
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => x.ClientId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.FamilyId);
            entity.HasIndex(x => x.ExpiresAtUtc);
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("productos");
            entity.HasKey(x => x.ProductoId);
            entity.Property(x => x.ProductoId).HasColumnName("producto_id");
            entity.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TipoMedidaBase).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.TipoComercialMayor).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.UnidadesPorCaja).IsRequired(false);
            entity.Property(x => x.UnidadesPorBulto).IsRequired(false);
            entity.Property(x => x.Activo).IsRequired();

            entity.HasIndex(x => x.Sku).IsUnique();
            entity.HasIndex(x => x.Nombre);
        });

        modelBuilder.Entity<Combo>(entity =>
        {
            entity.ToTable("combos");
            entity.HasKey(x => x.ComboId);
            entity.Property(x => x.ComboId).HasColumnName("combo_id");
            entity.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();

            entity.OwnsMany(x => x.Items, item =>
            {
                item.ToTable("combo_items");
                item.WithOwner().HasForeignKey("ComboId");
                item.Property<Guid>("Id").ValueGeneratedOnAdd();
                item.HasKey("Id");
                item.Property(x => x.ProductoId).IsRequired();
                item.Property(x => x.Cantidad).HasColumnType("decimal(10,2)").IsRequired();
            });

            entity.Ignore(x => x.SedeIdsHabilitadas);
            entity.Property<List<Guid>>("_sedeIdsHabilitadas")
                .HasColumnName("sede_ids_habilitadas")
                .HasColumnType("uuid[]")
                .HasField("sedeIdsHabilitadas");

            entity.OwnsOne(x => x.PrecioTotal, money =>
            {
                money.Property(m => m.Amount).HasColumnName("precio_total").HasColumnType("decimal(12,2)").IsRequired();
                money.Property(m => m.Currency).HasColumnName("precio_total_moneda").HasMaxLength(3).IsRequired();
            });

            entity.OwnsOne(x => x.PrecioPromocional, money =>
            {
                money.Property(m => m.Amount).HasColumnName("precio_promocional").HasColumnType("decimal(12,2)").IsRequired(false);
                money.Property(m => m.Currency).HasColumnName("precio_promocional_moneda").HasMaxLength(3).IsRequired(false);
            });

            entity.HasIndex(x => x.Estado);
            entity.HasIndex(x => x.Codigo).IsUnique();
        });

        modelBuilder.Entity<PrecioProductoSede>(entity =>
        {
            entity.ToTable("precios_producto_sede");
            entity.HasKey(x => x.PrecioProductoSedeId);
            entity.Property(x => x.PrecioProductoSedeId).HasColumnName("precio_producto_sede_id");
            entity.Property(x => x.ProductoId).IsRequired();
            entity.Property(x => x.SedeId).IsRequired();

            entity.OwnsOne(x => x.Precio1Unidad, money =>
            {
                money.Property(m => m.Amount).HasColumnName("precio1_unidad").HasColumnType("decimal(12,2)").IsRequired();
                money.Property(m => m.Currency).HasColumnName("precio1_moneda").HasMaxLength(3).IsRequired();
            });

            entity.OwnsOne(x => x.Precio2CajaBultoPieza, money =>
            {
                money.Property(m => m.Amount).HasColumnName("precio2_caja_bulto_pieza").HasColumnType("decimal(12,2)").IsRequired();
                money.Property(m => m.Currency).HasColumnName("precio2_moneda").HasMaxLength(3).IsRequired();
            });

            entity.OwnsOne(x => x.Precio3MayorDesde2, money =>
            {
                money.Property(m => m.Amount).HasColumnName("precio3_mayor_desde2").HasColumnType("decimal(12,2)").IsRequired();
                money.Property(m => m.Currency).HasColumnName("precio3_moneda").HasMaxLength(3).IsRequired();
            });

            entity.OwnsOne(x => x.Precio4MayoristaNegociable, money =>
            {
                money.Property(m => m.Amount).HasColumnName("precio4_mayorista").HasColumnType("decimal(12,2)").IsRequired(false);
                money.Property(m => m.Currency).HasColumnName("precio4_moneda").HasMaxLength(3).IsRequired(false);
            });

            entity.Property(x => x.Precio4RequiereAcuerdo).IsRequired();
            entity.Property(x => x.VigenteDesde).IsRequired();
            entity.Property(x => x.VigenteHasta).IsRequired(false);

            entity.HasIndex(x => new { x.ProductoId, x.SedeId, x.VigenteDesde });
        });

        modelBuilder.Entity<ApiToken>(entity =>
        {
            entity.ToTable("api_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Activo).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();

            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.Activo);
        });
    }
}
