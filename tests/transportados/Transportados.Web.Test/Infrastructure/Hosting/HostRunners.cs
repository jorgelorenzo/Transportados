using System.Diagnostics;

namespace Transportados.Web.Test.Infrastructure.Hosting;

public static class ApiHostRunner
{
    public static Task<HostRunHandle> StartAsync(ApiHostOptions options, CancellationToken cancellationToken)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = options.BaseUrl,
            ["ConnectionStrings__DefaultConnection"] = options.ConnectionString,
            ["Seeding__Enabled"] = "false"
        };

        return ProcessRunner.StartAsync(
            name: "api",
            command: "dotnet",
            arguments: $"run --project \"{options.ProjectPath}\" --configuration Debug --no-build --no-launch-profile",
            workingDirectory: options.WorkingDirectory,
            logDirectory: options.LogDirectory,
            environment: env,
            cancellationToken: cancellationToken);
    }
}

public static class WebHostRunner
{
    public static Task<HostRunHandle> StartAsync(WebHostOptions options, CancellationToken cancellationToken)
    {
        var httpsPort = new Uri(options.HttpsBaseUrl).Port;
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = $"{options.HttpBaseUrl};{options.HttpsBaseUrl}",
            ["ASPNETCORE_HTTPS_PORT"] = httpsPort.ToString(),
            ["ApiSettings__BaseUrl"] = $"{options.ApiBaseUrl.TrimEnd('/')}/",
            ["ApiSettings__ShowApiBaseUrlOnLogin"] = options.ShowApiBaseUrlOnLogin ? "true" : "false"
        };

        return ProcessRunner.StartAsync(
            name: "web",
            command: "dotnet",
            arguments: $"run --project \"{options.ProjectPath}\" --configuration Debug --no-build --no-launch-profile",
            workingDirectory: options.WorkingDirectory,
            logDirectory: options.LogDirectory,
            environment: env,
            cancellationToken: cancellationToken);
    }
}

internal static class ProcessRunner
{
    public static async Task<HostRunHandle> StartAsync(
        string name,
        string command,
        string arguments,
        string workingDirectory,
        string logDirectory,
        IReadOnlyDictionary<string, string> environment,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(logDirectory);
        var stdoutLogPath = Path.Combine(logDirectory, $"{name}.stdout.log");
        var stderrLogPath = Path.Combine(logDirectory, $"{name}.stderr.log");

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var pair in environment)
        {
            psi.Environment[pair.Key] = pair.Value;
        }

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start process '{name}'.");
        }

        var stdoutWriter = new StreamWriter(File.Open(stdoutLogPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
        var stderrWriter = new StreamWriter(File.Open(stderrLogPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stdoutWriter)
                {
                    stdoutWriter.WriteLine(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stderrWriter)
                {
                    stderrWriter.WriteLine(e.Data);
                }
            }
        };

        process.Exited += (_, _) =>
        {
            stdoutWriter.Dispose();
            stderrWriter.Dispose();
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        if (process.HasExited)
        {
            throw new InvalidOperationException($"Process '{name}' exited too early. Check logs under '{logDirectory}'.");
        }

        return new HostRunHandle
        {
            Name = name,
            Process = process,
            StdOutLogPath = stdoutLogPath,
            StdErrLogPath = stderrLogPath
        };
    }
}
