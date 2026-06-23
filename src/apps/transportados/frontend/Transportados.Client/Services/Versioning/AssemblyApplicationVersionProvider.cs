using System.Reflection;

namespace Transportados.Client.Services.Versioning;

public sealed class AssemblyApplicationVersionProvider : IApplicationVersionProvider
{
    public string DisplayVersion => GetApplicationVersion();

    private static string GetApplicationVersion()
    {
        var informationalVersion = typeof(AssemblyApplicationVersionProvider).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataSeparatorIndex = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            return metadataSeparatorIndex > 0
                ? informationalVersion[..metadataSeparatorIndex]
                : informationalVersion;
        }

        return typeof(AssemblyApplicationVersionProvider).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
