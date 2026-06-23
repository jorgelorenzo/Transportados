using Transportados.Web.Test.Infrastructure;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Tests.Smoke;

public sealed class PlatformTenantPhysicalDeleteTests : UiScenarioTestBase
{
    public PlatformTenantPhysicalDeleteTests(E2eTestFixture fixture)
        : base(fixture)
    {
    }

    [E2eFact]
    [Trait("Category", "UI")]
    public Task Superadmin_ShouldPhysicallyDeleteTenantOnlyAfterExactNameConfirmation() =>
        RunScenarioAsync(SeedProfile.TenantPhysicalDelete, async (page, execution) =>
        {
            await page.SetViewportSizeAsync(1280, 900);
            await LoginAsAsync(page, execution.WebBaseUrl, execution.SeedResult.Users["superadmin"]);

            await page.GotoAsync($"{execution.WebBaseUrl.TrimEnd('/')}/platform/tenants");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(page.GetByText("Gestion de tenants y aprobacion de altas pendientes.", new PageGetByTextOptions { Exact = true }))
                .ToBeVisibleAsync();
            await Expect(page.GetByLabel("Borrar definitivamente Transportados")).ToBeVisibleAsync();
            await CaptureEvidenceScreenshotAsync(page, execution, "01-platform-tenants-list");

            await page.GetByLabel("Borrar definitivamente Transportados").ClickAsync();

            var dialog = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Borrado definitivo" });
            var confirmButton = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Confirmar borrado definitivo de Transportados" });
            var confirmationInput = page.GetByLabel("Nombre exacto de la empresa");

            await Expect(dialog).ToBeVisibleAsync();
            await Expect(confirmButton).ToBeDisabledAsync();
            await CaptureEvidenceScreenshotAsync(page, execution, "02-delete-modal-empty-disabled");

            await confirmationInput.FillAsync("transportados");
            await Expect(confirmButton).ToBeDisabledAsync();
            await CaptureEvidenceScreenshotAsync(page, execution, "03-delete-modal-case-mismatch-disabled");

            await confirmationInput.FillAsync("Transportados");
            await Expect(confirmButton).ToBeEnabledAsync();
            await CaptureEvidenceScreenshotAsync(page, execution, "04-delete-modal-exact-name-enabled");

            await confirmButton.ClickAsync();

            await Expect(page.GetByText("Empresa borrada definitivamente.", new PageGetByTextOptions { Exact = true }))
                .ToBeVisibleAsync();
            await Expect(page.GetByLabel("Borrar definitivamente Transportados")).ToHaveCountAsync(0);
            await CaptureEvidenceScreenshotAsync(page, execution, "05-platform-tenants-after-delete");
            await AssertNoHorizontalOverflowAsync(page);
        });
}
