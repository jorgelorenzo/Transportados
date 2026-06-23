using System.Runtime.CompilerServices;
using Transportados.Web.Test.Infrastructure;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Tests;

[Collection(E2eCollection.CollectionName)]
public abstract class UiScenarioTestBase
{
    private const float DefaultInteractionTimeoutMilliseconds = 30000;
    private readonly E2eTestFixture _fixture;

    protected UiScenarioTestBase(E2eTestFixture fixture)
    {
        _fixture = fixture;
    }

    protected async Task RunScenarioAsync(
        SeedProfile profile,
        Func<IPage, TestExecutionContext, Task> assertion,
        bool showApiBaseUrlOnLogin = false,
        [CallerMemberName] string testMethodName = "",
        CancellationToken cancellationToken = default)
    {
        var testName = $"{GetType().Name}-{testMethodName}-{profile}";
        Console.WriteLine($"[E2E] Starting test: {GetType().Name}.{testMethodName} (profile={profile})");
        await using var execution = await _fixture.StartScenarioAsync(
            profile,
            testName,
            showApiBaseUrlOnLogin,
            cancellationToken);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !ShouldShowUi()
        });

        await using var browserContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            RecordVideoDir = execution.ArtifactsDirectory
        });

        var page = await browserContext.NewPageAsync();
        SetDefaultExpectTimeout(DefaultInteractionTimeoutMilliseconds);
        page.SetDefaultTimeout(DefaultInteractionTimeoutMilliseconds);
        page.SetDefaultNavigationTimeout(DefaultInteractionTimeoutMilliseconds);

        await page.Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        try
        {
            await assertion(page, execution);
        }
        catch
        {
            var screenshotPath = Path.Combine(execution.ArtifactsDirectory, "failure.png");
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });
            throw;
        }
        finally
        {
            var tracePath = Path.Combine(execution.ArtifactsDirectory, "trace.zip");
            await page.Context.Tracing.StopAsync(new TracingStopOptions
            {
                Path = tracePath
            });
        }
    }

    protected static async Task LoginAsAsync(IPage page, string baseUrl, SeededUserCredentials user)
    {
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/login");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await page.GetByLabel("Usuario").FillAsync(user.Username);
        await page.GetByLabel("Clave").FillAsync(user.Password);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Ingresar" }).ClickAsync();

        await Expect(page).Not.ToHaveURLAsync(new Regex(".*/login.*$", RegexOptions.IgnoreCase), new()
        {
            Timeout = DefaultInteractionTimeoutMilliseconds
        });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    protected static async Task AssertNoHorizontalOverflowAsync(IPage page)
    {
        var widths = await page.EvaluateAsync<int[]>(
            @"() => [
                window.innerWidth,
                document.documentElement.scrollWidth,
                document.body.scrollWidth
            ]");

        var innerWidth = widths[0];
        var documentScrollWidth = widths[1];
        var bodyScrollWidth = widths[2];
        Assert.True(
            documentScrollWidth <= innerWidth + 1,
            $"Expected document scrollWidth ({documentScrollWidth}) to fit viewport width ({innerWidth}).");
        Assert.True(
            bodyScrollWidth <= innerWidth + 1,
            $"Expected body scrollWidth ({bodyScrollWidth}) to fit viewport width ({innerWidth}).");
    }

    protected static async Task CaptureEvidenceScreenshotAsync(
        IPage page,
        TestExecutionContext execution,
        string name)
    {
        var fileName = $"{SanitizeForArtifactName(name)}.png";
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(execution.ArtifactsDirectory, fileName),
            FullPage = true
        });
    }

    private static string SanitizeForArtifactName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        return new string(chars).Replace(' ', '-');
    }

    private static bool ShouldShowUi()
    {
        var rawValue = Environment.GetEnvironmentVariable("TRANSPORTADOS_E2E_SHOW_UI");
        return rawValue is not null
            && (rawValue.Equals("1", StringComparison.OrdinalIgnoreCase)
                || rawValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                || rawValue.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
