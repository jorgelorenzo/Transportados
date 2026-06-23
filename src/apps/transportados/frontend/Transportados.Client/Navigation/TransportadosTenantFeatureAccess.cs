using System.Security.Claims;
using Transportados.Contracts.Api.Dto;

namespace Transportados.Client.Navigation;

public static class TransportadosTenantFeatureAccess
{
    public const string ClaimPrefix = "TenantFeature:";
    public const string Appearance = "Appearance";
    public const string Email = "Email";

    public const string AppearancePolicy = ClaimPrefix + Appearance;
    public const string EmailPolicy = ClaimPrefix + Email;

    public static IEnumerable<Claim> ToClaims(TenantFeatureFlagsDto features)
    {
        yield return new Claim(ClaimPrefix + Appearance, features.Appearance.ToString());
        yield return new Claim(ClaimPrefix + Email, features.Email.ToString());
    }

    public static bool IsEnabled(ClaimsPrincipal user, string feature)
    {
        var claim = user.FindFirst(ClaimPrefix + feature);
        if (claim == null)
        {
            return true;
        }

        return bool.TryParse(claim.Value, out var enabled) && enabled;
    }

    public static bool IsEnabled(TenantFeatureFlagsDto features, string feature) =>
        feature switch
        {
            Appearance => features.Appearance,
            Email => features.Email,
            _ => true
        };
}
