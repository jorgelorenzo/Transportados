using Transportados.Domain.Api.Domain;

namespace Transportados.Client.Localization;

public static class TransportadosDisplayLabels
{
    public static string TenantStatus(TenantStatus status) =>
        status switch
        {
            Transportados.Domain.Api.Domain.TenantStatus.Pending => "Pendiente",
            Transportados.Domain.Api.Domain.TenantStatus.Active => "Activo",
            Transportados.Domain.Api.Domain.TenantStatus.Disabled => "Deshabilitado",
            _ => "Sin estado"
        };

    public static string Role(string? role, string? techRoleLabel = null) =>
        role switch
        {
            Roles.SuperAdmin => "Superadmin",
            Roles.Admin => "Admin",
            Roles.Supervisor => "Supervisor",
            Roles.Tech => ResolveTechRoleLabel(techRoleLabel),
            _ => string.IsNullOrWhiteSpace(role) ? "-" : role
        };

    public static string ResolveTechRoleLabel(string? techRoleLabel) =>
        string.IsNullOrWhiteSpace(techRoleLabel) ? Roles.DefaultTechRoleLabel : techRoleLabel.Trim();
}
