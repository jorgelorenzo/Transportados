using System.Security.Claims;
using Newtonsoft.Json;
using Transportados.Platform.Core;

namespace Transportados.Platform.AspNetCore
{
    public static class PlatformClaimsPrincipalReader
    {
        public static PlatformUserClaimSet? ReadUser(ClaimsPrincipal principal, string adminRole)
        {
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return null;
            }

            var tenantMembershipsJson = principal.FindFirst(PlatformClaimTypes.TenantMemberships)?.Value;
            var tenantMemberships = !string.IsNullOrEmpty(tenantMembershipsJson)
                ? JsonConvert.DeserializeObject<List<PlatformTenantMemberClaim>>(tenantMembershipsJson) ?? new List<PlatformTenantMemberClaim>()
                : new List<PlatformTenantMemberClaim>();

            var state = TenantContextResolver.ResolveFromClaimsPrincipal(principal, adminRole);

            return new PlatformUserClaimSet
            {
                Id = userId,
                Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
                FullName = principal.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
                IsSuperAdmin = principal.FindFirst(PlatformClaimTypes.IsSuperAdmin)?.Value == "true",
                IsDemo = principal.FindFirst(PlatformClaimTypes.IsDemo)?.Value == "true",
                TenantMemberships = tenantMemberships,
                AllowedTenantIds = state.AllowedTenantIds,
                ActiveRole = state.ActiveRole,
                ActiveTenantId = state.ActiveTenantId,
                DefaultRole = principal.FindFirst(PlatformClaimTypes.DefaultRole)?.Value,
                AppContext = principal.FindFirst(PlatformClaimTypes.AppContext)?.Value
            };
        }
    }
}
