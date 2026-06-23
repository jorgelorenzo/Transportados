using Transportados.Domain.Api.Domain;

namespace Transportados.Contracts.Api.Dto;

public sealed class AuthenticatedContextDto
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
    public List<TenantMemberInfoDto> TenantMemberships { get; set; } = [];
    public List<Guid> AllowedTenantIds { get; set; } = [];
    public TenantFeatureFlagsDto ActiveTenantFeatures { get; set; } = new();
    public string TechRoleLabel { get; set; } = Roles.DefaultTechRoleLabel;
    public string? Role => IsSuperAdmin ? Roles.SuperAdmin : ActiveRole ?? TenantMemberships.FirstOrDefault()?.Role;
}
