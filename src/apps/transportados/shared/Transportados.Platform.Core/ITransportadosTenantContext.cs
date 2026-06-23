namespace Transportados.Platform.Core
{
    public interface ITransportadosTenantContext
    {
        List<Guid> AllowedTenantIds { get; }
        bool IsSuperAdmin { get; }
    }
}
