namespace Transportados.Platform.Core
{
    public sealed class RoleContextSelection
    {
        public string? ActiveRole { get; init; }
        public Guid? ActiveTenantId { get; init; }
        public PlatformTenantMemberClaim? SelectedMembership { get; init; }
        public bool UsesDefaultRole { get; init; }
    }
}
