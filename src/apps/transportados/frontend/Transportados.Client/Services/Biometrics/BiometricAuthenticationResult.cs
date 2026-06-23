namespace Transportados.Client.Services.Biometrics;

public sealed record BiometricAuthenticationResult(
    BiometricAuthenticationStatus Status,
    string? ErrorMessage = null)
{
    public static BiometricAuthenticationResult Succeeded { get; } =
        new(BiometricAuthenticationStatus.Succeeded);
}
