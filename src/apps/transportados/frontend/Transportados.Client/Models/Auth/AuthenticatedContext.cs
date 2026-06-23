using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;

namespace Transportados.Client.Models.Auth;

public sealed class AuthenticatedContext
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public bool IsDemo { get; set; }
    public string? ActiveRole { get; set; }
    public Guid? ActiveTenantId { get; set; }
    public string? AppContext { get; set; }
    public string? DefaultRole { get; set; }
    public List<TenantMemberInfo> TenantMemberships { get; set; } = [];
    public List<Guid> AllowedTenantIds { get; set; } = [];
    public TenantFeatureFlagsDto ActiveTenantFeatures { get; set; } = new();
    public string TechRoleLabel { get; set; } = Roles.DefaultTechRoleLabel;
    public string? Role => IsSuperAdmin
        ? Roles.SuperAdmin
        : Roles.Normalize(ActiveRole ?? TenantMemberships.FirstOrDefault()?.Role);
}

public sealed class TenantMemberInfo
{
    public Guid TenantMemberId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
