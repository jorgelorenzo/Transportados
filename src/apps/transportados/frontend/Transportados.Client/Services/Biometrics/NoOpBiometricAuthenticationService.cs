namespace Transportados.Client.Services.Biometrics;

public sealed class NoOpBiometricAuthenticationService : IBiometricAuthenticationService
{
    public Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(BiometricAvailability.NotAvailable);

    public Task<BiometricAuthenticationResult> AuthenticateAsync(
        BiometricAuthenticationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new BiometricAuthenticationResult(BiometricAuthenticationStatus.NotAvailable));
}
