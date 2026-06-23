namespace Transportados.Domain.Api.Domain;

public enum TransportadosPermission
{
    TenantHome,
    CustomerManagement,
    CustomerLookup,
    SettingsRead,
    SettingsManagement,
    UserManagement,
    UserApiManagement,
    Platform,
    AuthenticatedProfile,
    AuthenticatedAbout
}

public static class TransportadosAccessMatrix
{
    public const string TenantCoreRolesCsv = Roles.Admin + "," + Roles.Supervisor + "," + Roles.Tech;
    public const string TenantManagerRolesCsv = Roles.Admin + "," + Roles.Supervisor;
    public const string AdminRolesCsv = Roles.Admin;
    public const string PlatformRolesCsv = Roles.SuperAdmin;
    public const string HomeRolesCsv = TenantCoreRolesCsv + "," + Roles.SuperAdmin;

    public static readonly string[] TenantCoreRoles = [Roles.Admin, Roles.Supervisor, Roles.Tech];
    public static readonly string[] TenantManagerRoles = [Roles.Admin, Roles.Supervisor];
    public static readonly string[] AdminRoles = [Roles.Admin];
    public static readonly string[] PlatformRoles = [Roles.SuperAdmin];

    public static bool AllowsTenantRole(TransportadosPermission permission, string? activeRole)
    {
        if (string.IsNullOrWhiteSpace(activeRole))
        {
            return false;
        }

        return RolesFor(permission).Any(role => string.Equals(role, activeRole, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Allows(
        TransportadosPermission permission,
        string? activeRole,
        bool isSuperAdmin,
        Guid? activeTenantId)
    {
        if (permission is TransportadosPermission.AuthenticatedProfile or TransportadosPermission.AuthenticatedAbout)
        {
            return true;
        }

        if (permission == TransportadosPermission.Platform)
        {
            return isSuperAdmin;
        }

        if (isSuperAdmin && activeTenantId.HasValue && activeTenantId.Value != Guid.Empty)
        {
            return true;
        }

        return AllowsTenantRole(permission, activeRole);
    }

    public static IReadOnlyList<string> RolesFor(TransportadosPermission permission) =>
        permission switch
        {
            TransportadosPermission.TenantHome => TenantCoreRoles,
            TransportadosPermission.CustomerLookup => TenantCoreRoles,
            TransportadosPermission.SettingsRead => TenantCoreRoles,
            TransportadosPermission.CustomerManagement => TenantManagerRoles,
            TransportadosPermission.SettingsManagement => TenantManagerRoles,
            TransportadosPermission.UserApiManagement => TenantManagerRoles,
            TransportadosPermission.UserManagement => AdminRoles,
            TransportadosPermission.Platform => PlatformRoles,
            TransportadosPermission.AuthenticatedProfile => [],
            TransportadosPermission.AuthenticatedAbout => [],
            _ => []
        };
}
