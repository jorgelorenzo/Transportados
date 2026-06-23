using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Transportados.Domain.Api.Domain;
using Transportados.Persistence.DataAccess;
using Transportados.Platform.Core;

namespace Transportados.Persistence.Seeding;

public interface ITransportadosSeedService
{
    Task SeedInitialDataAsync();
}

public sealed class TransportadosSeedService(
    TransportadosDbContext dbContext,
    PasswordHashingManager passwordHashingManager,
    IOptions<SeedingOptions> options) : ITransportadosSeedService
{
    private const string DemoPassword = "transportados-demo";
    private const string AdminPassword = "admin";
    private const string CustomerBogusLocale = "es";
    private const int CustomerSeedOffset = 730600;
    private static readonly SeedLocation[] ArgentineLocations =
    [
        new("Buenos Aires", "Buenos Aires", "11"),
        new("La Plata", "Buenos Aires", "221"),
        new("Mar del Plata", "Buenos Aires", "223"),
        new("Cordoba", "Cordoba", "351"),
        new("Rosario", "Santa Fe", "341"),
        new("Santa Fe", "Santa Fe", "342"),
        new("Mendoza", "Mendoza", "261"),
        new("San Rafael", "Mendoza", "260"),
        new("Neuquen", "Neuquen", "299"),
        new("Cipolletti", "Rio Negro", "299"),
        new("Bariloche", "Rio Negro", "294"),
        new("Salta", "Salta", "387"),
        new("Tucuman", "Tucuman", "381"),
        new("Parana", "Entre Rios", "343"),
        new("Corrientes", "Corrientes", "379"),
        new("Posadas", "Misiones", "376")
    ];
    private readonly SeedingOptions _options = options.Value ?? new SeedingOptions();

    public async Task SeedInitialDataAsync()
    {
        await EnsureSeedUser(
            fullName: "Superadmin Transportados",
            email: "superadmin@transportados.com",
            password: "superadmin",
            isSuperAdmin: true);

        if (await dbContext.Tenants.IgnoreQueryFilters().AnyAsync())
        {
            await dbContext.SaveChangesAsync();
            return;
        }

        if (_options.SeedTransportados)
        {
            await SeedTenant(new SeedTenantSpec(
                Name: "Transportados",
                Slug: "transportados",
                Theme: "Orange",
                AdminEmail: "admin_transportados@transportados.com",
                CustomerCount: 100,
                DemoUserDisplayOrderOffset: 0));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedTenant(SeedTenantSpec spec)
    {
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == spec.Slug);
        if (tenant == null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Slug = spec.Slug,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Tenants.Add(tenant);
        }

        tenant.Name = spec.Name;
        tenant.Status = TenantStatus.Active;
        tenant.IsDemo = true;
        tenant.Deleted = false;
        tenant.FeatureAppearanceEnabled = true;
        tenant.FeatureEmailEnabled = true;

        var admin = await EnsureSeedUser($"Admin {spec.Name}", spec.AdminEmail, AdminPassword, isSuperAdmin: false);
        admin.DefaultRole = Roles.Admin;
        admin.LastActiveTenantId = tenant.Id;
        await EnsureSeedMember(admin, tenant, Roles.Admin);
        await EnsureDemoUser(admin, AdminPassword, spec.DemoUserDisplayOrderOffset + 1);

        var support = await EnsureSeedUser(
            $"Soporte {spec.Name}",
            $"soporte_{spec.Slug}@transportados.com",
            DemoPassword,
            isSuperAdmin: false);
        support.DefaultRole = Roles.Supervisor;
        support.LastActiveTenantId = tenant.Id;
        await EnsureSeedMember(support, tenant, Roles.Supervisor);
        await EnsureDemoUser(support, DemoPassword, spec.DemoUserDisplayOrderOffset + 2);

        var operatorUser = await EnsureSeedUser(
            $"Operador {spec.Name}",
            $"operador_{spec.Slug}@transportados.com",
            DemoPassword,
            isSuperAdmin: false);
        operatorUser.DefaultRole = Roles.Tech;
        operatorUser.LastActiveTenantId = tenant.Id;
        await EnsureSeedMember(operatorUser, tenant, Roles.Tech);
        await EnsureDemoUser(operatorUser, DemoPassword, spec.DemoUserDisplayOrderOffset + 3);

        await EnsureSettings(tenant, spec);
        await EnsureCustomers(tenant.Id, spec.CustomerCount);
    }

    private async Task<User> EnsureSeedUser(string fullName, string email, string password, bool isSuperAdmin)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
        }

        user.FullName = fullName;
        user.IsSuperAdmin = isSuperAdmin;
        user.DefaultRole = isSuperAdmin ? Roles.SuperAdmin : user.DefaultRole;
        user.Deleted = false;
        await EnsurePassword(user, password);
        return user;
    }

    private async Task EnsurePassword(User user, string password)
    {
        var existing = await dbContext.UserPasswords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.UserId == user.Id);
        if (existing == null)
        {
            dbContext.UserPasswords.Add(new UserPassword
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                HashedPassword = passwordHashingManager.HashToString(password)
            });
            return;
        }

        existing.Deleted = false;
        existing.HashedPassword = passwordHashingManager.HashToString(password);
    }

    private async Task EnsureSeedMember(User user, Tenant tenant, string role)
    {
        var member = await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.UserId == user.Id && item.TenantId == tenant.Id);
        if (member == null)
        {
            member = new TenantMember
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenant.Id
            };
            dbContext.TenantMembers.Add(member);
        }

        member.Role = role;
        member.Deleted = false;
    }

    private async Task EnsureDemoUser(User user, string password, int displayOrder)
    {
        var demoUser = await dbContext.DemoUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.UserId == user.Id);
        if (demoUser == null)
        {
            demoUser = new DemoUser
            {
                Id = Guid.NewGuid(),
                UserId = user.Id
            };
            dbContext.DemoUsers.Add(demoUser);
        }

        demoUser.PlainPassword = password;
        demoUser.DisplayOrder = displayOrder;
        demoUser.Deleted = false;
    }

    private async Task EnsureSettings(Tenant tenant, SeedTenantSpec spec)
    {
        var settings = await dbContext.Settings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenant.Id);
        if (settings == null)
        {
            settings = new Settings
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id
            };
            dbContext.Settings.Add(settings);
        }

        settings.Name = tenant.Name;
        settings.AppTheme = spec.Theme;
        settings.TechRoleLabel = Roles.DefaultTechRoleLabel;
        settings.Deleted = false;
    }

    private async Task EnsureCustomers(Guid tenantId, int count)
    {
        var currentCount = await dbContext.Customers
            .IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId && !item.Deleted);
        for (var index = currentCount + 1; index <= count; index++)
        {
            dbContext.Customers.Add(CreateSeedCustomer(tenantId, index));
        }
    }

    private static Customer CreateSeedCustomer(Guid tenantId, int index)
    {
        var location = ArgentineLocations[(index - 1) % ArgentineLocations.Length];
        var faker = new Faker<Customer>(CustomerBogusLocale)
            .UseSeed(CustomerSeedOffset + index)
            .RuleFor(customer => customer.Id, f => f.Random.Guid())
            .RuleFor(customer => customer.TenantId, _ => tenantId)
            .RuleFor(customer => customer.FullName, f => f.Name.FullName())
            .RuleFor(customer => customer.Code, _ => $"C{index:00000}")
            .RuleFor(customer => customer.Email, f => f.Internet.Email(provider: "example.com").ToLowerInvariant())
            .RuleFor(customer => customer.Phone, f => $"+54 {location.AreaCode} {f.Random.Number(100, 999)}-{f.Random.Number(1000, 9999)}")
            .RuleFor(customer => customer.City, _ => location.City)
            .RuleFor(customer => customer.State, _ => location.State)
            .RuleFor(customer => customer.AddressLine1, f => f.Address.StreetAddress())
            .RuleFor(customer => customer.Notes, _ => "Cliente demo generado por Bogus con localidades argentinas.");

        return faker.Generate();
    }

    private sealed record SeedTenantSpec(
        string Name,
        string Slug,
        string Theme,
        string AdminEmail,
        int CustomerCount,
        int DemoUserDisplayOrderOffset);

    private sealed record SeedLocation(string City, string State, string AreaCode);
}
