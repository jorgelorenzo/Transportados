using Transportados.Domain.Api.Domain;
using Transportados.Persistence.DataAccess;
using Transportados.Platform.AspNetCore;

namespace Transportados.Api.Infrastructure;

public sealed class HttpTenantContext : ITenantContext
{
    public List<Guid> AllowedTenantIds { get; } = [];
    public bool IsSuperAdmin { get; }

    public HttpTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        var state = TenantContextResolver.ResolveFromClaimsPrincipal(
            httpContextAccessor.HttpContext?.User,
            Roles.Admin);

        IsSuperAdmin = state.IsSuperAdmin;
        if (state.ActiveTenantId.HasValue && state.ActiveTenantId.Value != Guid.Empty)
        {
            AllowedTenantIds.Add(state.ActiveTenantId.Value);
            return;
        }

        if (!state.IsSuperAdmin)
        {
            AllowedTenantIds.AddRange(state.AllowedTenantIds);
        }
    }
}
