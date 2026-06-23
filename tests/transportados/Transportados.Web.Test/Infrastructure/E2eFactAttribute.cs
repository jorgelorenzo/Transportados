namespace Transportados.Web.Test.Infrastructure;

public sealed class E2eFactAttribute : FactAttribute
{
    public E2eFactAttribute()
    {
        if (!IsE2eEnabled())
        {
            Skip = "Transportados E2E tests are disabled. Use ops/scripts/run-ui-tests.ps1 to run them.";
        }
    }

    private static bool IsE2eEnabled()
    {
        var rawValue = Environment.GetEnvironmentVariable("TRANSPORTADOS_E2E_ENABLED");
        return rawValue is not null
            && (rawValue.Equals("1", StringComparison.OrdinalIgnoreCase)
                || rawValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                || rawValue.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}
