using Microsoft.EntityFrameworkCore;
using Transportados.Domain.Api.Domain;

namespace Transportados.Persistence.DataAccess;

public sealed class TransportadosDbContext : DbContext
{
    private readonly List<Guid> allowedTenantIds;

    public TransportadosDbContext(DbContextOptions<TransportadosDbContext> options)
        : base(options)
    {
        allowedTenantIds = [];
    }

    public TransportadosDbContext(DbContextOptions<TransportadosDbContext> options, ITenantContext? tenantContext)
        : base(options)
    {
        allowedTenantIds = tenantContext?.AllowedTenantIds ?? [];
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPassword> UserPasswords => Set<UserPassword>();
    public DbSet<UserVerificationCode> UserVerificationCodes => Set<UserVerificationCode>();
    public DbSet<DemoUser> DemoUsers => Set<DemoUser>();
    public DbSet<DemoTenantCredential> DemoTenantCredentials => Set<DemoTenantCredential>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Settings> Settings => Set<Settings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RegistrationContactFullName).HasMaxLength(200);
            entity.Property(e => e.RegistrationContactEmail).HasMaxLength(256);
            entity.Property(e => e.RegistrationContactPhone).HasMaxLength(50);
            entity.Property(e => e.RegistrationAddressLine).HasMaxLength(500);
            entity.Property(e => e.RegistrationCity).HasMaxLength(100);
            entity.Ignore(e => e.IsActive);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Photo).HasMaxLength(500);
            entity.Property(e => e.DefaultRole).HasMaxLength(200);
            entity.HasIndex(e => e.LastActiveTenantId);
            entity.Ignore(e => e.ListText);
        });

        modelBuilder.Entity<UserPassword>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.HashedPassword).IsRequired().HasMaxLength(500);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserVerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.VerificationCode).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<DemoUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.Property(e => e.PlainPassword).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DemoTenantCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.PlainPassword).IsRequired().HasMaxLength(200);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TenantMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.TenantId, e.Role }).IsUnique();
            entity.HasIndex(e => e.TenantId);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Code });
            entity.HasIndex(e => new { e.TenantId, e.Email });
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.AddressLine1).HasMaxLength(500);
            entity.Property(e => e.AddressLine2).HasMaxLength(500);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Code).HasMaxLength(100);
            entity.Property(e => e.Latitud).HasMaxLength(50);
            entity.Property(e => e.Longitud).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Ignore(e => e.FullAddress);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Settings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.AppTheme).HasMaxLength(50);
            entity.Property(e => e.TechRoleLabel).IsRequired().HasMaxLength(80).HasDefaultValue(Roles.DefaultTechRoleLabel);
            entity.Ignore(e => e.IsSmtpEnabled);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureTenantQueryFilters(modelBuilder);
    }

    private void ConfigureTenantQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantMember>().HasQueryFilter(e => allowedTenantIds.Contains(e.TenantId));
        modelBuilder.Entity<Customer>().HasQueryFilter(e => allowedTenantIds.Contains(e.TenantId));
        modelBuilder.Entity<Settings>().HasQueryFilter(e => allowedTenantIds.Contains(e.TenantId));
        modelBuilder.Entity<DemoTenantCredential>().HasQueryFilter(e => allowedTenantIds.Contains(e.TenantId));
    }
}
