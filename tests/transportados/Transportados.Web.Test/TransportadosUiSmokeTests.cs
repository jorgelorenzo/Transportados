using System.Security.Claims;
using Transportados.Client.Navigation;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;

namespace Transportados.Web.Test;

public sealed class TransportadosUiSmokeTests
{
    [Fact]
    public void NavigationMatrix_ShowsOnlyCustomerAndSupportItemsForTenantAdmin()
    {
        var user = CreateTenantUser(Roles.Admin);

        var hrefs = TransportadosNavigationMatrix.GetVisibleItems(user)
            .Select(item => item.Href)
            .ToList();

        Assert.Contains("customers", hrefs);
        Assert.Contains("settings", hrefs);
        Assert.Contains("users", hrefs);
        Assert.DoesNotContain("workorders", hrefs);
        Assert.DoesNotContain("budgets", hrefs);
        Assert.DoesNotContain("materials", hrefs);
        Assert.DoesNotContain("areas", hrefs);
        Assert.DoesNotContain("document-nodes", hrefs);
        Assert.DoesNotContain("platform/industry-templates", hrefs);
    }

    [Fact]
    public void NavigationMatrix_SuperAdminWithoutTenantStartsAtOrganizations()
    {
        var context = new Transportados.Client.Models.Auth.AuthenticatedContext
        {
            UserId = Guid.NewGuid(),
            Email = "superadmin@test.local",
            FullName = "Superadmin",
            IsSuperAdmin = true,
            ActiveRole = Roles.SuperAdmin
        };

        Assert.Equal("/platform/tenants", TransportadosNavigationMatrix.ResolveInitialTarget(context));
    }

    [Fact]
    public void TenantFeatureAccess_OnlyEmitsSupportFeatureClaims()
    {
        var claims = TransportadosTenantFeatureAccess.ToClaims(new TenantFeatureFlagsDto
        {
            Appearance = true,
            Email = false
        }).Select(claim => claim.Type).ToList();

        Assert.Equal(
            ["TenantFeature:Appearance", "TenantFeature:Email"],
            claims);
        Assert.DoesNotContain("TenantFeature:Import", claims);
        Assert.DoesNotContain("TenantFeature:Indicators", claims);
        Assert.DoesNotContain("TenantFeature:Calendar", claims);
        Assert.DoesNotContain("TenantFeature:Budgets", claims);
        Assert.DoesNotContain("TenantFeature:Materials", claims);
    }

    private static ClaimsPrincipal CreateTenantUser(string role)
    {
        var tenantId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")),
                new Claim(ClaimTypes.Email, "admin@test.local"),
                new Claim(ClaimTypes.Role, role),
                new Claim("ActiveRole", role),
                new Claim("ActiveTenantId", tenantId.ToString("D"))
            ],
            "test");

        return new ClaimsPrincipal(identity);
    }
}
