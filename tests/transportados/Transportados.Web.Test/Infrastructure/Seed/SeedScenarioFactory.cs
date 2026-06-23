namespace Transportados.Web.Test.Infrastructure.Seed;

public static class SeedScenarioFactory
{
    public static SeedingOptions CreateOptions(SeedProfile profile) =>
        profile switch
        {
            SeedProfile.AuthSmoke => new SeedingOptions
            {
                Enabled = true,
                SeedTransportados = true
            },
            SeedProfile.ResponsiveShell => new SeedingOptions
            {
                Enabled = true,
                SeedTransportados = true
            },
            SeedProfile.TenantPhysicalDelete => new SeedingOptions
            {
                Enabled = true,
                SeedTransportados = true
            },
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown seed profile.")
        };

    public static SeedResult CreateResult(SeedProfile profile) =>
        profile switch
        {
            SeedProfile.AuthSmoke => AuthSmokeResult(),
            SeedProfile.ResponsiveShell => ResponsiveShellResult(),
            SeedProfile.TenantPhysicalDelete => TenantPhysicalDeleteResult(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown seed profile.")
        };

    private static SeedResult AuthSmokeResult()
    {
        var result = new SeedResult
        {
            Profile = SeedProfile.AuthSmoke,
            TenantName = "Transportados"
        };
        result.Users["admin"] = new SeededUserCredentials("admin_transportados@transportados.com", "admin", Roles.Admin);
        result.Users["superadmin"] = new SeededUserCredentials("superadmin@transportados.com", "superadmin", Roles.SuperAdmin);
        return result;
    }

    private static SeedResult ResponsiveShellResult()
    {
        var result = new SeedResult
        {
            Profile = SeedProfile.ResponsiveShell,
            TenantName = "Transportados"
        };
        result.Users["admin"] = new SeededUserCredentials("admin_transportados@transportados.com", "admin", Roles.Admin);
        result.Users["tech"] = new SeededUserCredentials("operador_transportados@transportados.com", "transportados-demo", Roles.Tech);
        result.Users["superadmin"] = new SeededUserCredentials("superadmin@transportados.com", "superadmin", Roles.SuperAdmin);
        return result;
    }

    private static SeedResult TenantPhysicalDeleteResult()
    {
        var result = new SeedResult
        {
            Profile = SeedProfile.TenantPhysicalDelete,
            TenantName = "Transportados"
        };
        result.Users["admin"] = new SeededUserCredentials("admin_transportados@transportados.com", "admin", Roles.Admin);
        result.Users["superadmin"] = new SeededUserCredentials("superadmin@transportados.com", "superadmin", Roles.SuperAdmin);
        return result;
    }
}
