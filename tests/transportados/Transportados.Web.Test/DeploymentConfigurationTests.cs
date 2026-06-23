using System.Text.Json;

namespace Transportados.Web.Test;

public sealed class DeploymentConfigurationTests
{
    [Fact]
    public void GithubActionsWorkflows_TargetStagingComponentsAndHosts()
    {
        var apiWorkflow = ReadRepoFile(".github", "workflows", "transportados-api.yml");
        Assert.Contains("group: transportados-api-staging", apiWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("group: transportados-api-prod", apiWorkflow, StringComparison.Ordinal);
        Assert.Contains("-Environment staging -Component \"transportados-api-staging\"", apiWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("-AppPoolName \"transportados-api-staging\"", apiWorkflow, StringComparison.Ordinal);
        Assert.Contains("TRANSPORTADOS_API_STAGING_APP_POOL", apiWorkflow, StringComparison.Ordinal);
        Assert.Contains("transportados-api-staging.transportados.com", apiWorkflow, StringComparison.Ordinal);
        Assert.Contains("TRANSPORTADOS_API_STAGING_HOST", apiWorkflow, StringComparison.Ordinal);
        Assert.Contains("transportados-api-staging.transportados.com", apiWorkflow, StringComparison.Ordinal);

        var webWorkflow = ReadRepoFile(".github", "workflows", "transportados-web.yml");
        Assert.Contains("group: transportados-web-staging", webWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("group: transportados-web-prod", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("-Environment staging -Component \"transportados-web-staging\"", webWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("-AppPoolName \"transportados-web-staging\"", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("TRANSPORTADOS_WEB_STAGING_APP_POOL", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("transportados-app-staging.transportados.com", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("TRANSPORTADOS_API_STAGING_BASE_URL", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("src/apps/transportados/frontend/Transportados.Client/**", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("TRANSPORTADOS_WEB_STAGING_HOST", webWorkflow, StringComparison.Ordinal);
        Assert.Contains("transportados-app-staging.transportados.com", webWorkflow, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(GetRepoRoot(), ".github", "workflows", "landing.yml")));
    }

    [Fact]
    public void IisDeployScripts_DefaultToStagingSafeNamesWhenEnvironmentIsStaging()
    {
        var apiScript = ReadRepoFile(".github", "scripts", "deploy-transportados-api.ps1");
        Assert.Contains("Resolve-StagingName", apiScript, StringComparison.Ordinal);
        Assert.Contains("$Component = Resolve-StagingName -Name $Component -Environment $Environment", apiScript, StringComparison.Ordinal);
        Assert.DoesNotContain("$AppPoolName = Resolve-StagingName -Name $AppPoolName -Environment $Environment", apiScript, StringComparison.Ordinal);
        Assert.Contains("Resolve-StagingAppPoolName", apiScript, StringComparison.Ordinal);
        Assert.Contains("transportados-api-staging.transportados.com", apiScript, StringComparison.Ordinal);

        var webScript = ReadRepoFile(".github", "scripts", "deploy-transportados-web.ps1");
        Assert.Contains("Resolve-StagingName", webScript, StringComparison.Ordinal);
        Assert.DoesNotContain("$AppPoolName = Resolve-StagingName -Name $AppPoolName -Environment $Environment", webScript, StringComparison.Ordinal);
        Assert.Contains("Resolve-StagingAppPoolName", webScript, StringComparison.Ordinal);
        Assert.Contains("transportados-app-staging.transportados.com", webScript, StringComparison.Ordinal);
        Assert.Contains("ApiSettings__BaseUrl", webScript, StringComparison.Ordinal);
        Assert.Contains("ApiSettings__ShowApiBaseUrlOnLogin", webScript, StringComparison.Ordinal);
        Assert.Contains("Deployment__EnvironmentName", webScript, StringComparison.Ordinal);
        Assert.Contains("https://transportados-api-staging.transportados.com/", webScript, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(GetRepoRoot(), ".github", "scripts", "deploy-transportados-landing.ps1")));
    }

    [Fact]
    public void VmBlazorDeployScript_ExposesDeploymentEnvironmentName()
    {
        var script = ReadRepoFile(".github", "scripts", "deploy-transportados-web.ps1");

        Assert.Contains("\"Deployment__EnvironmentName\" = $Environment", script, StringComparison.Ordinal);
        Assert.Contains("\"ApiSettings__ShowApiBaseUrlOnLogin\" = $showApiBaseUrlOnLogin", script, StringComparison.Ordinal);
        Assert.Contains("Transportados Web deployment environment injected into web.config", script, StringComparison.Ordinal);
        Assert.Contains("Transportados Web login API base URL display flag injected into web.config", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicDeployEnvironmentMap_SeparatesStagingFromProductionRoutes()
    {
        using var document = JsonDocument.Parse(ReadRepoFile(".codex", "skills", "transportados-deploy-public", "references", "public-environments.json"));
        var environments = document.RootElement.GetProperty("environments");
        var staging = environments.GetProperty("staging").GetProperty("components");
        var prod = environments.GetProperty("prod").GetProperty("components");

        AssertComponent(
            staging.GetProperty("backend"),
            "~/apps/transportados-api-staging",
            "transportados-api-staging",
            "https://transportados-api-staging.transportados.com/api/health");
        AssertComponent(
            staging.GetProperty("frontend"),
            "~/apps/transportados-web-staging",
            "transportados-web-staging",
            "https://transportados-app-staging.transportados.com");
        Assert.False(staging.TryGetProperty("landing", out _));

        AssertComponent(
            prod.GetProperty("backend"),
            "~/apps/transportados-api",
            "transportados-api",
            "https://transportados-api.transportados.com/api/health");
        AssertComponent(
            prod.GetProperty("frontend"),
            "~/apps/transportados-web",
            "transportados-web",
            "https://transportados-app.transportados.com");
        Assert.False(prod.TryGetProperty("landing", out _));
    }

    private static void AssertComponent(JsonElement component, string remotePath, string serviceName, string healthUrl)
    {
        Assert.Equal(remotePath, component.GetProperty("remotePath").GetString());
        Assert.Equal(serviceName, component.GetProperty("serviceName").GetString());
        Assert.Equal(healthUrl, component.GetProperty("healthUrl").GetString());
    }

    private static string ReadRepoFile(params string[] relativeParts)
    {
        return File.ReadAllText(Path.Combine(GetRepoRoot(), Path.Combine(relativeParts)));
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Transportados.App.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
