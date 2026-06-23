using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Transportados.Client.Models.Auth;
using Transportados.Client.Services.Auth;

namespace Transportados.Client.Navigation;

public sealed class TransportadosNavigationService(
    NavigationManager navigation,
    AuthService authService)
{
    public async Task<string> ResolveInitialTargetAsync()
    {
        var context = await authService.GetStoredContextAsync();
        return ResolveInitialTarget(context);
    }

    public string ResolveInitialTarget(AuthenticatedContext? context) =>
        TransportadosNavigationMatrix.ResolveInitialTarget(context);

    public async Task<bool> TryNavigateToInitialTargetAsync(bool replace = false)
    {
        var target = await ResolveInitialTargetAsync();
        return TryNavigateTo(target, replace);
    }

    public bool TryNavigateToInitialTarget(AuthenticatedContext? context, bool replace = false)
    {
        var target = ResolveInitialTarget(context);
        return TryNavigateTo(target, replace);
    }

    public async Task NavigateToPostLoginTargetAsync(bool replace = true)
    {
        var target = await ResolvePostLoginTargetAsync();
        navigation.NavigateTo(target, replace);
    }

    public async Task<bool> TryRestoreSessionOrNavigateToLoginAsync()
    {
        if (await authService.TryRestoreSessionAsync())
        {
            return true;
        }

        NavigateToLoginWithReturnUrl();
        return false;
    }

    public async Task RedirectUnauthorizedRouteAsync()
    {
        var relativePath = navigation.ToBaseRelativePath(navigation.Uri);
        if (string.Equals(NormalizePath(relativePath), "login", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (await authService.TryRestoreSessionAsync())
        {
            navigation.NavigateTo(ToAppPath(relativePath), replace: true);
            return;
        }

        NavigateToLoginWithReturnUrl(relativePath);
    }

    public async Task NavigateHomeAsync()
    {
        if (!await TryNavigateToInitialTargetAsync())
        {
            navigation.NavigateTo("/", replace: false);
        }
    }

    public void NavigateToLoginWithReturnUrl(string? relativePath = null)
    {
        var target = ToAppPath(relativePath ?? navigation.ToBaseRelativePath(navigation.Uri));
        if (IsPublicAuthTarget(target))
        {
            target = "/";
        }

        var encodedReturnUrl = Uri.EscapeDataString(target);
        navigation.NavigateTo($"/login?returnUrl={encodedReturnUrl}", replace: true);
    }

    private async Task<string> ResolvePostLoginTargetAsync()
    {
        var context = await authService.GetStoredContextAsync();
        var initialTarget = ResolveInitialTarget(context);
        var returnUrl = GetReturnUrl();

        if (string.Equals(returnUrl, "/", StringComparison.Ordinal) ||
            IsPublicAuthTarget(returnUrl))
        {
            return initialTarget;
        }

        return returnUrl;
    }

    private bool TryNavigateTo(string target, bool replace)
    {
        if (string.Equals(target, "/", StringComparison.Ordinal))
        {
            return false;
        }

        navigation.NavigateTo(target, replace);
        return true;
    }

    private string GetReturnUrl()
    {
        var uri = navigation.ToAbsoluteUri(navigation.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("returnUrl", out var returnUrl))
        {
            var parsed = returnUrl.ToString();
            if (!string.IsNullOrWhiteSpace(parsed) && parsed.StartsWith('/'))
            {
                return parsed;
            }
        }

        return "/";
    }

    private static bool IsPublicAuthTarget(string target)
    {
        var normalizedTarget = NormalizePath(target);
        return normalizedTarget is "login" or "register" or "password-recovery" or "first-login" or "logout";
    }

    private static string ToAppPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/";
        }

        return $"/{relativePath.TrimStart('/')}";
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
}
