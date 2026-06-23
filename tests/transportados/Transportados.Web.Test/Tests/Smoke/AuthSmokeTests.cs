using Transportados.Web.Test.Infrastructure;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Tests.Smoke;

public sealed class AuthSmokeTests : UiScenarioTestBase
{
    public AuthSmokeTests(E2eTestFixture fixture)
        : base(fixture)
    {
    }

    [E2eFact]
    [Trait("Category", "UI")]
    public Task AdminLogin_ShouldReachAuthenticatedArea() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            await LoginAsAsync(page, execution.WebBaseUrl, execution.SeedResult.Users["admin"]);

            await Expect(page).ToHaveURLAsync(new Regex(".*/customers/?$", RegexOptions.IgnoreCase));
            await Expect(page.GetByLabel("Nuevo cliente")).ToBeVisibleAsync();
            await Expect(page.GetByLabel("Importar clientes desde CSV")).ToHaveCountAsync(0);
            await AssertNoHorizontalOverflowAsync(page);
        });

    [E2eFact]
    [Trait("Category", "UI")]
    public Task InvalidLogin_ShouldStayOnLoginAndShowError() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            var admin = execution.SeedResult.Users["admin"];

            await page.GotoAsync($"{execution.WebBaseUrl.TrimEnd('/')}/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await page.GetByLabel("Usuario").FillAsync(admin.Username);
            await page.GetByLabel("Clave").FillAsync("password-incorrecta");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Ingresar" }).ClickAsync();

            await Expect(page).ToHaveURLAsync(new Regex(".*/login.*$", RegexOptions.IgnoreCase));
            await Expect(page.GetByText("Credenciales invalidas.", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await AssertNoHorizontalOverflowAsync(page);
        });

    [E2eFact]
    [Trait("Category", "UI")]
    public Task Login_WhenApiBaseUrlDisplayFlagIsEnabled_ShouldShowConfiguredApiUrl() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            var expectedApiUrl = $"{execution.ApiBaseUrl.TrimEnd('/')}/";

            await page.GotoAsync($"{execution.WebBaseUrl.TrimEnd('/')}/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(page.GetByText($"API: {expectedApiUrl}", new PageGetByTextOptions { Exact = true }))
                .ToBeVisibleAsync();
            await AssertNoHorizontalOverflowAsync(page);
        }, showApiBaseUrlOnLogin: true);

    [E2eFact]
    [Trait("Category", "UI")]
    public Task Login_WhenApiBaseUrlDisplayFlagIsDisabled_ShouldNotShowConfiguredApiUrl() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            var expectedApiUrl = $"{execution.ApiBaseUrl.TrimEnd('/')}/";

            await page.GotoAsync($"{execution.WebBaseUrl.TrimEnd('/')}/login");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(page.GetByText($"API: {expectedApiUrl}", new PageGetByTextOptions { Exact = true }))
                .Not.ToBeVisibleAsync();
            await AssertNoHorizontalOverflowAsync(page);
        });

    [E2eFact]
    [Trait("Category", "UI")]
    public Task Logout_ShouldReturnToLogin() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            await LoginAsAsync(page, execution.WebBaseUrl, execution.SeedResult.Users["admin"]);

            await page.GetByLabel("Logout").ClickAsync();

            await Expect(page).ToHaveURLAsync(new Regex(".*/login.*$", RegexOptions.IgnoreCase));
            await Expect(page.GetByText("Transportados", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await AssertNoHorizontalOverflowAsync(page);
        });
}
