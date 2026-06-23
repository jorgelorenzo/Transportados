#if USE_FAKE_BIOMETRICS
using Android.Util;
using Transportados.Client.Services.Biometrics;

namespace Transportados.Mobile.Services;

public sealed class FakeMauiBiometricAuthenticationService : IBiometricAuthenticationService
{
    private const string LogTag = "TRANSPORTADOS_FAKE_BIOMETRICS";
    private static int _availabilityChecks;
    private static int _authenticationRequests;

    public Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = Interlocked.Increment(ref _availabilityChecks);
        Log.Warn(LogTag, $"Availability #{count}: returning Available");
        return Task.FromResult(BiometricAvailability.Available);
    }

    public async Task<BiometricAuthenticationResult> AuthenticateAsync(
        BiometricAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref _authenticationRequests);
        Log.Warn(LogTag, $"Authenticate #{count}: reason={request.Reason}, title={request.Title}");

        await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);

        Log.Warn(LogTag, $"Authenticate #{count}: returning Succeeded");
        return BiometricAuthenticationResult.Succeeded;
    }
}
#endif
