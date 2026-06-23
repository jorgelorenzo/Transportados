namespace Transportados.Platform.Core
{
    public sealed class TenantContextState : ITransportadosTenantContext
    {
        public List<Guid> AllowedTenantIds { get; set; } = new();
        public bool IsSuperAdmin { get; set; }
        public string? ActiveRole { get; set; }
        public Guid? ActiveTenantId { get; set; }
    }
}
