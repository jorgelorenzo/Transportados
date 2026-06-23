using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Transportados.Domain.Api.Domain;
using Transportados.Client.Models.Auth;
using Transportados.Client.Navigation;
using Transportados.Client.Services.Storage;

namespace Transportados.Client.Services.Auth;

public sealed class CustomAuthStateProvider(IClientStorageService storage) : AuthenticationStateProvider, IAuthStateNotifier
{
    private const string AuthContextKey = "authContext";
    private const string IsAuthenticatedKey = "isAuthenticated";
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var isAuthenticatedRaw = await storage.GetAsync(IsAuthenticatedKey);
            var isAuthenticated = bool.TryParse(isAuthenticatedRaw, out var parsed) && parsed;
            if (!isAuthenticated)
            {
                return new AuthenticationState(Anonymous);
            }

            var context = await GetStoredContextAsync();
            return context == null
                ? new AuthenticationState(Anonymous)
                : new AuthenticationState(BuildPrincipal(context));
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public void NotifyUserAuthentication(AuthenticatedContext userInfo)
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(BuildPrincipal(userInfo))));
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    private async Task<AuthenticatedContext?> GetStoredContextAsync()
    {
        var rawContext = await storage.GetAsync(AuthContextKey);
        if (string.IsNullOrWhiteSpace(rawContext))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AuthenticatedContext>(rawContext);
    }

    private static ClaimsPrincipal BuildPrincipal(AuthenticatedContext userInfo)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userInfo.UserId.ToString("D")),
            new(ClaimTypes.Email, userInfo.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(userInfo.FullName) ? userInfo.Email : userInfo.FullName)
        };

        if (!string.IsNullOrWhiteSpace(userInfo.ActiveRole))
        {
            claims.Add(new Claim(ClaimTypes.Role, userInfo.ActiveRole));
            claims.Add(new Claim("ActiveRole", userInfo.ActiveRole));
        }

        if (userInfo.IsSuperAdmin)
        {
            claims.Add(new Claim("IsSuperAdmin", bool.TrueString));
            if (!string.Equals(userInfo.ActiveRole, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, Roles.SuperAdmin));
            }
        }

        if (userInfo.ActiveTenantId.HasValue)
        {
            claims.Add(new Claim("ActiveTenantId", userInfo.ActiveTenantId.Value.ToString("D")));
        }

        if (!string.IsNullOrWhiteSpace(userInfo.AppContext))
        {
            claims.Add(new Claim("AppContext", userInfo.AppContext));
        }

        claims.AddRange(TransportadosTenantFeatureAccess.ToClaims(userInfo.ActiveTenantFeatures));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Transportados.CustomAuth"));
    }
}
