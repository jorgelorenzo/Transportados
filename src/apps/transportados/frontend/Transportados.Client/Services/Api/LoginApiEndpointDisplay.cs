namespace Transportados.Client.Services.Api;

public static class LoginApiEndpointDisplay
{
    public static string? BuildLabel(bool showApiBaseUrlOnLogin, string apiBaseUrl)
    {
        if (!showApiBaseUrlOnLogin || string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return null;
        }

        var normalizedApiBaseUrl = apiBaseUrl.Trim();
        if (!normalizedApiBaseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedApiBaseUrl += "/";
        }

        return $"API: {normalizedApiBaseUrl}";
    }
}
