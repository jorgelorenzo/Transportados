using System.Security.Claims;
using Transportados.Platform.Core;

namespace Transportados.Platform.AspNetCore
{
    public static class TenantContextResolver
    {
        public static TenantContextState ResolveFromClaimsPrincipal(ClaimsPrincipal? principal, string adminRole)
        {
            var state = new TenantContextState();
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return state;
            }

            state.IsSuperAdmin = principal.FindFirst(PlatformClaimTypes.IsSuperAdmin)?.Value == "true";
            state.ActiveRole = principal.FindFirst(PlatformClaimTypes.ActiveRole)?.Value;
            state.ActiveTenantId = Guid.TryParse(principal.FindFirst(PlatformClaimTypes.ActiveTenantId)?.Value, out var activeTenantId)
                ? activeTenantId
                : null;

            var tenantIdsClaim = principal.FindFirst(PlatformClaimTypes.AllowedTenantIds)?.Value;
            if (!string.IsNullOrEmpty(tenantIdsClaim))
            {
                state.AllowedTenantIds = tenantIdsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => Guid.TryParse(s, out _))
                    .Select(Guid.Parse)
                    .ToList();
            }

            if (!state.IsSuperAdmin &&
                string.Equals(state.ActiveRole, adminRole, StringComparison.OrdinalIgnoreCase) &&
                state.ActiveTenantId.HasValue)
            {
                state.AllowedTenantIds = new List<Guid> { state.ActiveTenantId.Value };
            }

            return state;
        }
    }
}
