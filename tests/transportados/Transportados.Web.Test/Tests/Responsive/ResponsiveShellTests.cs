using Transportados.Web.Test.Infrastructure;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Tests.Responsive;

public sealed class ResponsiveShellTests : UiScenarioTestBase
{
    public ResponsiveShellTests(E2eTestFixture fixture)
        : base(fixture)
    {
    }

    [E2eFact]
    [Trait("Category", "UI")]
    public Task MobileLogin_ShouldShowCustomersContentWithClosedDrawer() =>
        RunScenarioAsync(SeedProfile.ResponsiveShell, async (page, execution) =>
        {
            await page.SetViewportSizeAsync(390, 844);

            await LoginAsAsync(page, execution.WebBaseUrl, execution.SeedResult.Users["admin"]);

            await Expect(page).ToHaveURLAsync(new Regex(".*/customers/?$", RegexOptions.IgnoreCase));
            await Expect(page.GetByLabel("Nuevo cliente")).ToBeVisibleAsync();
            await Expect(page.GetByLabel("Navegacion principal")).ToBeHiddenAsync();
            await AssertNoHorizontalOverflowAsync(page);
        });

    [E2eFact]
    [Trait("Category", "UI")]
    public Task MobileDrawer_ShouldOpenNavigateAndCloseWithoutHorizontalOverflow() =>
        RunScenarioAsync(SeedProfile.ResponsiveShell, async (page, execution) =>
        {
            await page.SetViewportSizeAsync(390, 844);

            await LoginAsAsync(page, execution.WebBaseUrl, execution.SeedResult.Users["admin"]);

            var navigation = page.GetByLabel("Navegacion principal");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Abrir menu" }).ClickAsync();
            await Expect(navigation).ToBeVisibleAsync();

            await navigation.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Configuracion", Exact = true }).ClickAsync();

            await Expect(page).ToHaveURLAsync(new Regex(".*/settings/?$", RegexOptions.IgnoreCase));
            await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Configuracion", Exact = true })).ToBeVisibleAsync();
            await Expect(navigation).ToBeHiddenAsync();
            await AssertNoHorizontalOverflowAsync(page);
        });

    [E2eFact]
    [Trait("Category", "UI")]
    public Task DesktopCustomers_ShouldKeepSidebarVisible() =>
        RunScenarioAsync(SeedProfile.ResponsiveShell, async (page, execution) =>
        {
            await page.SetViewportSizeAsync(1280, 720);

            await LoginAsAsync(page, execution.WebBaseUrl, execution.SeedResult.Users["admin"]);

            var navigation = page.GetByLabel("Navegacion principal");
            var customerMarker = page.GetByLabel("Nuevo cliente");

            await Expect(navigation).ToBeVisibleAsync();
            await Expect(customerMarker).ToBeVisibleAsync();

            var navigationBox = await navigation.BoundingBoxAsync();
            var customerBox = await customerMarker.BoundingBoxAsync();
            Assert.NotNull(navigationBox);
            Assert.NotNull(customerBox);
            Assert.True(
                customerBox.X >= navigationBox.X + navigationBox.Width - 1,
                "Expected desktop customer content to render beside the sidebar.");

            await AssertNoHorizontalOverflowAsync(page);
        });
}
