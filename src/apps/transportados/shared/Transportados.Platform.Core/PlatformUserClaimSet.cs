namespace Transportados.Platform.Core
{
    public sealed class PlatformUserClaimSet
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsSuperAdmin { get; set; }
        public bool IsDemo { get; set; }
        public List<PlatformTenantMemberClaim> TenantMemberships { get; set; } = new();
        public List<Guid> AllowedTenantIds { get; set; } = new();
        public string? ActiveRole { get; set; }
        public Guid? ActiveTenantId { get; set; }
        public string? DefaultRole { get; set; }
        public string? AppContext { get; set; }
    }
}
