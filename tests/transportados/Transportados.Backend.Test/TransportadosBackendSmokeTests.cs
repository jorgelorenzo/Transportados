using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Persistence.DataAccess;
using Transportados.Persistence.Seeding;
using Transportados.Platform.Core;

namespace Transportados.Backend.Test;

public sealed class TransportadosBackendSmokeTests
{
    [Fact]
    public void DomainModel_ContainsOnlyCustomerAsBusinessEntity()
    {
        var domainTypeNames = typeof(Customer).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(Customer).Namespace &&
                !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var allowedDomainTypes = new[]
        {
            nameof(Identity),
            nameof(ITenant),
            nameof(TenantStatus),
            nameof(TenantFeatureFlag),
            nameof(Tenant),
            nameof(Roles),
            nameof(User),
            nameof(UserPassword),
            nameof(UserVerificationCode),
            nameof(DemoUser),
            nameof(DemoTenantCredential),
            nameof(TenantMember),
            nameof(Customer),
            nameof(Settings),
            nameof(TransportadosPermission),
            nameof(TransportadosAccessMatrix)
        };

        Assert.Equal(
            allowedDomainTypes.Order(StringComparer.Ordinal),
            domainTypeNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AccessMatrix_RemovesOperationalPermissions()
    {
        var permissionNames = Enum.GetNames<TransportadosPermission>();

        Assert.Contains(nameof(TransportadosPermission.CustomerManagement), permissionNames);
        Assert.Contains(nameof(TransportadosPermission.SettingsManagement), permissionNames);
        Assert.Contains(nameof(TransportadosPermission.UserManagement), permissionNames);
        Assert.DoesNotContain("WorkOrders", permissionNames);
        Assert.DoesNotContain("Budgets", permissionNames);
        Assert.DoesNotContain("MaterialCatalogManagement", permissionNames);
        Assert.DoesNotContain("AreaManagement", permissionNames);
        Assert.DoesNotContain("DocumentNodeManagement", permissionNames);
    }

    [Fact]
    public async Task SeedInitialData_CreatesSupportUsersAndCustomersOnly()
    {
        var databaseName = $"transportados-seed-reduced-{Guid.NewGuid():N}";
        await CreateSeedService(databaseName).SeedInitialDataAsync();

        await using var dbContext = CreateDbContext(databaseName);
        var tenant = Assert.Single(await dbContext.Tenants.IgnoreQueryFilters().ToListAsync());
        Assert.Equal("Transportados", tenant.Name);
        Assert.Equal("transportados", tenant.Slug);
        Assert.NotNull(await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Email == "superadmin@transportados.com"));
        Assert.NotNull(await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Email == "admin_transportados@transportados.com"));
        Assert.NotNull(await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Email == "soporte_transportados@transportados.com"));
        Assert.NotNull(await dbContext.Users.IgnoreQueryFilters().SingleOrDefaultAsync(user => user.Email == "operador_transportados@transportados.com"));
        Assert.True(await dbContext.Customers.IgnoreQueryFilters().AnyAsync());
        Assert.True(await dbContext.Settings.IgnoreQueryFilters().AnyAsync());
    }

    [Fact]
    public async Task DataService_Customers_ArePagedAndTenantScoped()
    {
        var databaseName = $"transportados-customers-{Guid.NewGuid():N}";
        var fixture = await CreateTenantFixture(databaseName);
        var service = CreateDataService(databaseName, fixture.Context);

        await service.SaveCustomer(new CustomerDto
        {
            FullName = "Acme SA",
            Code = "ACME",
            City = "Neuquen",
            Email = "contacto@acme.test"
        });

        await service.SaveCustomer(new CustomerDto
        {
            FullName = "Beta SRL",
            Code = "BETA",
            City = "Cipolletti"
        });

        var page = await service.GetCustomers(new CustomerQueryDto
        {
            Search = "ACME",
            Skip = 0,
            Take = 20
        });

        var customer = Assert.Single(page.Customers);
        Assert.Equal("Acme SA", customer.FullName);
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task DataService_Settings_KeepsSupportConfigurationOnly()
    {
        var databaseName = $"transportados-settings-{Guid.NewGuid():N}";
        var fixture = await CreateTenantFixture(databaseName);
        var service = CreateDataService(databaseName, fixture.Context);

        var settings = await service.SaveSettings(new SettingsDto
        {
            Name = "Nueva marca",
            TechRoleLabel = "Campo",
            SmtpHost = "smtp.test",
            SmtpPort = 587,
            SmtpUser = "user",
            SmtpPass = "pass",
            SmtpUseSSL = true
        });

        Assert.Equal("Nueva marca", settings.Name);
        Assert.True(settings.IsSmtpEnabled);
        Assert.DoesNotContain(typeof(SettingsDto).GetProperties(), property => property.Name.Contains("Cube", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("WorkOrder", typeof(SettingsDto).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain("Budget", typeof(SettingsDto).GetProperties().Select(property => property.Name));
    }

    private static TransportadosSeedService CreateSeedService(string databaseName) =>
        new(
            CreateDbContext(databaseName),
            new PasswordHashingManager(),
            Options.Create(new SeedingOptions
            {
                Enabled = true,
                SeedTransportados = true
            }));

    private static EFDataServices CreateDataService(string databaseName, AuthenticatedContextDto context)
    {
        var service = new EFDataServices(CreateDbContext(databaseName), new PasswordHashingManager())
        {
            LoggedUser = context
        };
        return service;
    }

    private static async Task<TenantFixture> CreateTenantFixture(string databaseName)
    {
        await using var dbContext = CreateDbContext(databaseName);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Tenant Test",
            Slug = "tenant-test",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin Test",
            Email = "admin@test.local",
            DefaultRole = Roles.Admin,
            LastActiveTenantId = tenant.Id
        };
        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = Roles.Admin
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        dbContext.TenantMembers.Add(member);
        dbContext.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = tenant.Name
        });
        await dbContext.SaveChangesAsync();

        return new TenantFixture(
            tenant.Id,
            user.Id,
            new AuthenticatedContextDto
            {
                UserId = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                ActiveRole = Roles.Admin,
                ActiveTenantId = tenant.Id,
                AllowedTenantIds = [tenant.Id],
                TenantMemberships =
                [
                    new TenantMemberInfoDto
                    {
                        TenantMemberId = member.Id,
                        TenantId = tenant.Id,
                        TenantName = tenant.Name,
                        Role = Roles.Admin
                    }
                ]
            });
    }

    private static TransportadosDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TransportadosDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new TransportadosDbContext(options);
    }

    private sealed record TenantFixture(Guid TenantId, Guid UserId, AuthenticatedContextDto Context);
}
