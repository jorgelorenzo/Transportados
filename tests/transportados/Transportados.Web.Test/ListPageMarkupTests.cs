namespace Transportados.Web.Test;

public sealed class ListPageMarkupTests
{
    [Fact]
    public void ServerPagedTablesAreNotWrappedInExtraPagePaper()
    {
        var pagesDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "apps",
            "transportados",
            "frontend",
            "Transportados.Client",
            "Components",
            "Pages");

        var offenders = Directory
            .EnumerateFiles(pagesDirectory, "*.razor", SearchOption.AllDirectories)
            .Where(path =>
            {
                var markup = File.ReadAllText(path);

                return Regex.IsMatch(
                    markup,
                    "<MudPaper\\b(?=[^>]*\\bClass=\"pa-4\")(?=[^>]*\\bElevation=\"1\")[^>]*>\\s*<ServerPagedTable\\b",
                    RegexOptions.Multiline);
            })
            .Select(path => Path.GetRelativePath(FindRepositoryRoot(), path))
            .Order()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Server-paged list tables should use the table surface only, without an extra page-level MudPaper wrapper: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void ServerPagedTables_ShouldHideMudTableSmallDeviceSortSelect()
    {
        var appCss = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "apps",
            "transportados",
            "frontend",
            "Transportados.Client",
            "wwwroot",
            "app.css"));

        Assert.Matches(
            "\\.transportados-server-paged-table\\s+\\.mud-table-smalldevices-sortselect\\s*\\{[^}]*display\\s*:\\s*none",
            appCss);
    }

    [Fact]
    public void PlatformTenants_ShouldNotExposeDemoCreationContract()
    {
        var platformTenantsPage = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "apps",
            "transportados",
            "frontend",
            "Transportados.Client",
            "Components",
            "Pages",
            "PlatformTenants.razor"));

        Assert.Contains("Usuarios", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Clientes", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear demo", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenants/seed", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ParameterizedCompanySeed", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Areas", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Presupuestos", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ordenes", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorkOrder", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Crear seed", platformTenantsPage, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Transportados.App.sln")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Could not locate Transportados repository root.");
        }

        return current.FullName;
    }
}
