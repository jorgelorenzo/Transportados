using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Transportados.Web.Test.Infrastructure.Hosting;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Infrastructure;

[CollectionDefinition(CollectionName)]
public sealed class E2eCollection : ICollectionFixture<E2eTestFixture>
{
    public const string CollectionName = "transportados-web-e2e";
}

public sealed class E2eTestFixture
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly string ApiProjectPath = Path.Combine(RepoRoot, "src", "apps", "transportados", "backend", "Transportados.Api", "Transportados.Api.csproj");
    private static readonly string ApiWorkingDirectory = Path.GetDirectoryName(ApiProjectPath)!;
    private static readonly string WebProjectPath = Path.Combine(RepoRoot, "src", "apps", "transportados", "frontend", "Transportados.Web", "Transportados.Web.csproj");
    private static readonly string WebWorkingDirectory = Path.GetDirectoryName(WebProjectPath)!;
    private static readonly string ArtifactRoot = Path.Combine(RepoRoot, "logs", "e2e");

    public async Task<TestExecutionContext> StartScenarioAsync(
        SeedProfile profile,
        string testName,
        bool showApiBaseUrlOnLogin = false,
        CancellationToken cancellationToken = default)
    {
        var testId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{SanitizeForPath(testName)}-{Guid.NewGuid():N}".ToLowerInvariant();
        var artifactsDirectory = Path.Combine(ArtifactRoot, testId);
        Directory.CreateDirectory(artifactsDirectory);

        var sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong!Passw0rd")
            .Build();

        var context = new TestExecutionContext
        {
            TestId = testId,
            ArtifactsDirectory = artifactsDirectory,
            SeedResult = SeedScenarioFactory.CreateResult(profile),
            ApiBaseUrl = string.Empty,
            WebBaseUrl = string.Empty
        };

        try
        {
            await sqlContainer.StartAsync(cancellationToken);
            context.RegisterResource(sqlContainer);

            var dbName = $"TransportadosE2E_{Guid.NewGuid():N}";
            var connectionString = BuildDatabaseConnectionString(sqlContainer.GetConnectionString(), dbName);

            var dbContext = CreateDbContext(connectionString);
            context.RegisterResource(dbContext);
            await dbContext.Database.MigrateAsync(cancellationToken);

            var seedService = new TransportadosSeedService(
                dbContext,
                new PasswordHashingManager(),
                Options.Create(SeedScenarioFactory.CreateOptions(profile)));
            await seedService.SeedInitialDataAsync();

            var apiPort = GetFreeTcpPort();
            var webHttpPort = GetFreeTcpPort();
            var webHttpsPort = GetFreeTcpPort();
            var apiBaseUrl = $"http://127.0.0.1:{apiPort}";
            var webHttpBaseUrl = $"http://127.0.0.1:{webHttpPort}";
            var webHttpsBaseUrl = $"https://127.0.0.1:{webHttpsPort}";

            var apiHandle = await ApiHostRunner.StartAsync(new ApiHostOptions
            {
                ProjectPath = ApiProjectPath,
                WorkingDirectory = ApiWorkingDirectory,
                BaseUrl = apiBaseUrl,
                ConnectionString = connectionString,
                LogDirectory = artifactsDirectory
            }, cancellationToken);
            context.RegisterResource(apiHandle);

            await WaitForHttpReadyAsync($"{apiBaseUrl}/api/health", TimeSpan.FromMinutes(2), cancellationToken);

            var webHandle = await WebHostRunner.StartAsync(new WebHostOptions
            {
                ProjectPath = WebProjectPath,
                WorkingDirectory = WebWorkingDirectory,
                HttpBaseUrl = webHttpBaseUrl,
                HttpsBaseUrl = webHttpsBaseUrl,
                ApiBaseUrl = apiBaseUrl,
                LogDirectory = artifactsDirectory,
                ShowApiBaseUrlOnLogin = showApiBaseUrlOnLogin
            }, cancellationToken);
            context.RegisterResource(webHandle);

            await WaitForHttpReadyAsync(webHttpsBaseUrl, TimeSpan.FromMinutes(2), cancellationToken, allowInvalidCertificate: true);

            var finalContext = new TestExecutionContext
            {
                TestId = testId,
                ArtifactsDirectory = artifactsDirectory,
                SeedResult = SeedScenarioFactory.CreateResult(profile),
                ApiBaseUrl = apiBaseUrl,
                WebBaseUrl = webHttpsBaseUrl
            };
            finalContext.TakeOwnershipFrom(context);
            return finalContext;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static TransportadosDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TransportadosDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new TransportadosDbContext(options, new SuperAdminTenantContext());
    }

    private static string BuildDatabaseConnectionString(string baseConnectionString, string dbName)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = baseConnectionString
        };

        builder["Database"] = dbName;
        builder["TrustServerCertificate"] = "True";
        return builder.ConnectionString;
    }

    private static async Task WaitForHttpReadyAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool allowInvalidCertificate = false)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using var handler = new HttpClientHandler();
        if (allowInvalidCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(url, cts.Token);
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect or HttpStatusCode.MovedPermanently)
                {
                    return;
                }
            }
            catch
            {
                // Retry until timeout.
            }

            await Task.Delay(500, cts.Token);
        }

        throw new TimeoutException($"Timeout waiting for endpoint '{url}'.");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string SanitizeForPath(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        return new string(chars).Replace(' ', '-');
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var appSolution = Path.Combine(current.FullName, "Transportados.App.sln");
            if (File.Exists(appSolution))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve the Transportados repository root from the E2E test output directory.");
    }
}

internal sealed class SuperAdminTenantContext : ITenantContext
{
    public List<Guid> AllowedTenantIds { get; } = [];
    public bool IsSuperAdmin => true;
}
