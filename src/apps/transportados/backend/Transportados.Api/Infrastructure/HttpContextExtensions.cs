using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Platform.AspNetCore;

namespace Transportados.Api.Infrastructure;

public static class HttpContextExtensions
{
    public static AuthenticatedContextDto? LoggedUser(this HttpContext context)
    {
        try
        {
            var claims = PlatformClaimsPrincipalReader.ReadUser(context.User, Roles.Admin);
            if (claims == null)
            {
                return null;
            }

            return new AuthenticatedContextDto
            {
                UserId = claims.Id,
                Email = claims.Email,
                FullName = claims.FullName,
                IsSuperAdmin = claims.IsSuperAdmin,
                IsDemo = claims.IsDemo,
                TenantMemberships = claims.TenantMemberships.Select(m => new TenantMemberInfoDto
                {
                    TenantMemberId = m.TenantMemberId,
                    TenantId = m.TenantId,
                    TenantName = m.TenantName,
                    Role = m.Role
                }).ToList(),
                AllowedTenantIds = claims.AllowedTenantIds,
                ActiveRole = claims.ActiveRole,
                ActiveTenantId = claims.ActiveTenantId,
                AppContext = claims.AppContext,
                DefaultRole = claims.DefaultRole,
                TechRoleLabel = Roles.DefaultTechRoleLabel
            };
        }
        catch
        {
            return null;
        }
    }
}
