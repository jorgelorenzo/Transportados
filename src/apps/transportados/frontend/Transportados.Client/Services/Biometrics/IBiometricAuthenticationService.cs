namespace Transportados.Client.Services.Biometrics;

public interface IBiometricAuthenticationService
{
    Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

    Task<BiometricAuthenticationResult> AuthenticateAsync(
        BiometricAuthenticationRequest request,
        CancellationToken cancellationToken = default);
}
