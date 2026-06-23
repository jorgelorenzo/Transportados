using Transportados.Web.Test.Infrastructure;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Tests.Smoke;

public sealed class PublicRegistrationSmokeTests : UiScenarioTestBase
{
    public PublicRegistrationSmokeTests(E2eTestFixture fixture)
        : base(fixture)
    {
    }

    [E2eFact]
    [Trait("Category", "UI")]
    public Task CompanyRegistration_ShouldEndPendingApprovalWithoutDemo() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            var unique = Guid.NewGuid().ToString("N")[..8];
            var companyName = $"Empresa QA {unique}";
            var contactEmail = $"alta-{unique}@transportados.local";

            await page.GotoAsync($"{execution.WebBaseUrl.TrimEnd('/')}/register");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Expect(page.GetByText("Alta de empresa", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();

            await page.GetByLabel("Nombre de la empresa").FillAsync(companyName);
            await page.GetByLabel("Responsable").FillAsync("Responsable QA");
            await page.GetByLabel("Email de contacto").FillAsync(contactEmail);
            await page.GetByLabel("Telefono").FillAsync("2994000000");
            await page.GetByLabel("Ciudad").FillAsync("Neuquen");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Solicitar alta" }).ClickAsync();

            await Expect(page.GetByText("Alta recibida. La empresa queda pendiente de revision.", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText(companyName, new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Estado pendiente", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Demo activa", new PageGetByTextOptions { Exact = true })).Not.ToBeVisibleAsync();
            await Expect(page.GetByText("30 dias", new PageGetByTextOptions { Exact = false })).Not.ToBeVisibleAsync();
            await AssertNoHorizontalOverflowAsync(page);
            await CaptureEvidenceScreenshotAsync(page, execution, "company-pending-registration");
        });

    [E2eFact]
    [Trait("Category", "UI")]
    public Task CompanyRegistration_WithInvalidEmail_ShouldShowValidationWithoutCreatingCompany() =>
        RunScenarioAsync(SeedProfile.AuthSmoke, async (page, execution) =>
        {
            var unique = Guid.NewGuid().ToString("N")[..8];

            await page.GotoAsync($"{execution.WebBaseUrl.TrimEnd('/')}/register");
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await page.GetByLabel("Nombre de la empresa").FillAsync($"Empresa Invalida {unique}");
            await page.GetByLabel("Responsable").FillAsync("Responsable Invalido");
            await page.GetByLabel("Email de contacto").FillAsync("email-invalido");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Solicitar alta" }).ClickAsync();

            await Expect(page.GetByText("Ingrese un email valido", new PageGetByTextOptions { Exact = true })).ToBeVisibleAsync();
            await Expect(page.GetByText("Demo activa", new PageGetByTextOptions { Exact = true })).Not.ToBeVisibleAsync();
            await Expect(page.GetByText("30 dias", new PageGetByTextOptions { Exact = false })).Not.ToBeVisibleAsync();
            await AssertNoHorizontalOverflowAsync(page);
            await CaptureEvidenceScreenshotAsync(page, execution, "company-invalid-email-validation");
        });

}
