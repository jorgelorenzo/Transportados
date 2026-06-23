using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Transportados.Platform.Core;

namespace Transportados.Platform.AspNetCore
{
    public static class PlatformJwtTokenBuilder
    {
        public static string Build(
            PlatformUserClaimSet userInfo,
            string? issuer,
            string? audience,
            string signingKey,
            string superAdminRole)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userInfo.Id.ToString()),
                new(ClaimTypes.Email, userInfo.Email),
                new(ClaimTypes.Name, userInfo.FullName),
                new(PlatformClaimTypes.IsSuperAdmin, userInfo.IsSuperAdmin.ToString().ToLowerInvariant()),
                new(PlatformClaimTypes.AllowedTenantIds, string.Join(",", userInfo.AllowedTenantIds))
            };

            if (userInfo.IsDemo)
            {
                claims.Add(new Claim(PlatformClaimTypes.IsDemo, "true"));
            }

            if (userInfo.TenantMemberships.Any())
            {
                claims.Add(new Claim(
                    PlatformClaimTypes.TenantMemberships,
                    JsonConvert.SerializeObject(userInfo.TenantMemberships)));
            }

            if (!string.IsNullOrWhiteSpace(userInfo.ActiveRole))
            {
                claims.Add(new Claim(ClaimTypes.Role, userInfo.ActiveRole));
                claims.Add(new Claim(PlatformClaimTypes.ActiveRole, userInfo.ActiveRole));
            }

            if (userInfo.ActiveTenantId.HasValue)
            {
                claims.Add(new Claim(PlatformClaimTypes.ActiveTenantId, userInfo.ActiveTenantId.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(userInfo.DefaultRole))
            {
                claims.Add(new Claim(PlatformClaimTypes.DefaultRole, userInfo.DefaultRole));
            }

            if (!string.IsNullOrWhiteSpace(userInfo.AppContext))
            {
                claims.Add(new Claim(PlatformClaimTypes.AppContext, userInfo.AppContext));
            }

            if (userInfo.IsSuperAdmin)
            {
                claims.Add(new Claim(ClaimTypes.Role, superAdminRole));
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                signingCredentials: credentials,
                claims: claims);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
