using System.Security.Claims;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using Transportados.Client.Models.Auth;
using Transportados.Domain.Api.Domain;

namespace Transportados.Client.Navigation;

public sealed record TransportadosNavigationItem(
    string Text,
    string Href,
    NavLinkMatch Match = NavLinkMatch.Prefix,
    string Icon = "");

public sealed record TransportadosNavigationTitle(string Text, bool IsMenuItem);

public static class TransportadosNavigationMatrix
{
    private static readonly (string PathPrefix, string Text)[] NonMenuTitleFallbacks =
    [
        new("profile", "Perfil"),
        new("about", "Acerca de"),
        new("not-found", "No encontrado"),
        new("error", "Error")
    ];

    private static readonly TransportadosNavigationItem CustomersItem =
        new("Clientes", "customers", Icon: Icons.Material.Filled.Contacts);

    private static readonly TransportadosNavigationItem SettingsItem =
        new("Configuracion", "settings", Icon: Icons.Material.Filled.Settings);

    private static readonly TransportadosNavigationItem UsersItem =
        new("Usuarios", "users", Icon: Icons.Material.Filled.People);

    private static readonly TransportadosNavigationItem PlatformTenantsItem =
        new("Organizaciones", "platform/tenants", Icon: Icons.Material.Filled.Business);

    public static IReadOnlyList<TransportadosNavigationItem> GetVisibleItems(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var items = new List<TransportadosNavigationItem>();
        var activeRole = ResolveActiveRole(user);
        var hasTenantContext = HasTenantContext(user);

        if (hasTenantContext && TransportadosAccessMatrix.AllowsTenantRole(TransportadosPermission.CustomerManagement, activeRole))
        {
            items.Add(CustomersItem);
        }

        if (hasTenantContext && TransportadosAccessMatrix.AllowsTenantRole(TransportadosPermission.SettingsRead, activeRole))
        {
            items.Add(SettingsItem);
        }

        if (hasTenantContext && TransportadosAccessMatrix.AllowsTenantRole(TransportadosPermission.UserManagement, activeRole))
        {
            items.Add(UsersItem);
        }

        if (IsSuperAdmin(user))
        {
            items.Add(PlatformTenantsItem);
        }

        return items;
    }

    public static string ResolveInitialTarget(AuthenticatedContext? context)
    {
        if (context == null)
        {
            return "/";
        }

        if (context.IsSuperAdmin && (!context.ActiveTenantId.HasValue || context.ActiveTenantId.Value == Guid.Empty))
        {
            return ToNavigationTarget(PlatformTenantsItem.Href);
        }

        if (context.ActiveTenantId.HasValue &&
            TransportadosAccessMatrix.Allows(
                TransportadosPermission.CustomerManagement,
                ResolveActiveRole(context),
                context.IsSuperAdmin,
                context.ActiveTenantId))
        {
            return ToNavigationTarget(CustomersItem.Href);
        }

        return "/";
    }

    public static string? ResolveCurrentTitle(ClaimsPrincipal user, string baseRelativePath) =>
        ResolveCurrentTitleInfo(user, baseRelativePath)?.Text;

    public static TransportadosNavigationTitle? ResolveCurrentTitleInfo(ClaimsPrincipal user, string baseRelativePath)
    {
        var path = NormalizePath(baseRelativePath);
        var navigationItem = GetVisibleItems(user)
            .Where(item => MatchesPath(item, path))
            .OrderByDescending(item => NormalizePath(item.Href).Length)
            .FirstOrDefault();

        if (navigationItem != null)
        {
            return new TransportadosNavigationTitle(navigationItem.Text, IsMenuItem: true);
        }

        if (string.IsNullOrEmpty(path))
        {
            return new TransportadosNavigationTitle("Inicio", IsMenuItem: true);
        }

        var fallbackTitle = NonMenuTitleFallbacks
            .Where(fallback => PathHasPrefix(path, fallback.PathPrefix))
            .OrderByDescending(fallback => fallback.PathPrefix.Length)
            .Select(fallback => fallback.Text)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(fallbackTitle)
            ? null
            : new TransportadosNavigationTitle(fallbackTitle, IsMenuItem: false);
    }

    private static string? ResolveActiveRole(ClaimsPrincipal user)
    {
        var activeRole = user.FindFirst("ActiveRole")?.Value;
        if (!string.IsNullOrWhiteSpace(activeRole))
        {
            return Roles.Normalize(activeRole) ?? activeRole;
        }

        return TransportadosAccessMatrix.TenantCoreRoles.FirstOrDefault(user.IsInRole);
    }

    private static string ResolveActiveRole(AuthenticatedContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ActiveRole))
        {
            return Roles.Normalize(context.ActiveRole) ?? context.ActiveRole;
        }

        if (!string.IsNullOrWhiteSpace(context.DefaultRole))
        {
            return Roles.Normalize(context.DefaultRole) ?? context.DefaultRole;
        }

        return Roles.Normalize(context.TenantMemberships.FirstOrDefault()?.Role) ?? string.Empty;
    }

    private static bool HasTenantContext(ClaimsPrincipal user)
    {
        if (!IsSuperAdmin(user))
        {
            return true;
        }

        var activeTenant = user.FindFirst("ActiveTenantId")?.Value;
        return Guid.TryParse(activeTenant, out var tenantId) && tenantId != Guid.Empty;
    }

    private static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.IsInRole(Roles.SuperAdmin) ||
        string.Equals(user.FindFirst("IsSuperAdmin")?.Value, bool.TrueString, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesPath(TransportadosNavigationItem item, string path)
    {
        var href = NormalizePath(item.Href);
        if (string.IsNullOrEmpty(href))
        {
            return string.IsNullOrEmpty(path);
        }

        if (item.Match == NavLinkMatch.All)
        {
            return string.Equals(path, href, StringComparison.OrdinalIgnoreCase);
        }

        return PathHasPrefix(path, href);
    }

    private static bool PathHasPrefix(string path, string pathPrefix)
    {
        var normalizedPrefix = NormalizePath(pathPrefix);
        if (string.IsNullOrEmpty(normalizedPrefix))
        {
            return string.IsNullOrEmpty(path);
        }

        return string.Equals(path, normalizedPrefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith($"{normalizedPrefix}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim();
        var queryOrHashIndex = normalized.IndexOfAny(new[] { '?', '#' });
        if (queryOrHashIndex >= 0)
        {
            normalized = normalized[..queryOrHashIndex];
        }

        return normalized.Trim('/');
    }

    private static string ToNavigationTarget(string href) =>
        string.IsNullOrWhiteSpace(href) ? "/" : $"/{href.TrimStart('/')}";
}
