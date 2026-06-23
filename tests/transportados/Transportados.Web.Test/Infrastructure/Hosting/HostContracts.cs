using System.Diagnostics;

namespace Transportados.Web.Test.Infrastructure.Hosting;

public sealed class ApiHostOptions
{
    public required string ProjectPath { get; init; }
    public required string WorkingDirectory { get; init; }
    public required string BaseUrl { get; init; }
    public required string ConnectionString { get; init; }
    public required string LogDirectory { get; init; }
}

public sealed class WebHostOptions
{
    public required string ProjectPath { get; init; }
    public required string WorkingDirectory { get; init; }
    public required string HttpBaseUrl { get; init; }
    public required string HttpsBaseUrl { get; init; }
    public required string ApiBaseUrl { get; init; }
    public required string LogDirectory { get; init; }
    public bool ShowApiBaseUrlOnLogin { get; init; } = false;
}

public sealed class HostRunHandle : IAsyncDisposable
{
    public required Process Process { get; init; }
    public required string Name { get; init; }
    public required string StdOutLogPath { get; init; }
    public required string StdErrLogPath { get; init; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                await Process.WaitForExitAsync();
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
        finally
        {
            Process.Dispose();
        }
    }
}
