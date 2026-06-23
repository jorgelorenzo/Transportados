using Transportados.Client.Services.Storage;
#if USE_FAKE_BIOMETRICS_SEED_RESTORE
using Android.Util;
#endif

namespace Transportados.Mobile.Services;

public sealed class MauiClientStorageService : IClientStorageService
{
#if USE_FAKE_BIOMETRICS_SEED_RESTORE
    private const string FakeBiometricsLogTag = "TRANSPORTADOS_FAKE_BIOMETRICS";
    private static int _fakeBiometricRestoreSeeded;
#endif

    public async Task SetAsync(string key, string value)
    {
        await SecureStorage.Default.SetAsync(key, value);
    }

    public async Task<string?> GetAsync(string key)
    {
#if USE_FAKE_BIOMETRICS_SEED_RESTORE
        await EnsureFakeBiometricRestoreSeedAsync();
#endif
        return await SecureStorage.Default.GetAsync(key);
    }

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }

#if USE_FAKE_BIOMETRICS_SEED_RESTORE
    private static async Task EnsureFakeBiometricRestoreSeedAsync()
    {
        if (Interlocked.Exchange(ref _fakeBiometricRestoreSeeded, 1) == 1)
        {
            return;
        }

        var existingAccessToken = await SecureStorage.Default.GetAsync("accessToken");
        if (!string.IsNullOrWhiteSpace(existingAccessToken))
        {
            Log.Warn(FakeBiometricsLogTag, "Seed skipped: existing accessToken found");
            return;
        }

        await SecureStorage.Default.SetAsync("accessToken", "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJleHAiOjQxMDI0NDQ4MDB9.signature");
        await SecureStorage.Default.SetAsync("isAuthenticated", bool.TrueString);
        await SecureStorage.Default.SetAsync("biometricAuthenticationEnabled", bool.TrueString);
        await SecureStorage.Default.SetAsync("userId", "22222222-2222-2222-2222-222222222222");
        await SecureStorage.Default.SetAsync("userEmail", "fake-biometrics@transportados.local");
        await SecureStorage.Default.SetAsync("activeRole", "Admin");
        await SecureStorage.Default.SetAsync("activeTenantId", "11111111-1111-1111-1111-111111111111");
        await SecureStorage.Default.SetAsync("appContext", "transportados");
        await SecureStorage.Default.SetAsync(
            "authContext",
            """
            {
              "UserId": "22222222-2222-2222-2222-222222222222",
              "Email": "fake-biometrics@transportados.local",
              "FullName": "Fake Biometrics",
              "ActiveRole": "Admin",
              "ActiveTenantId": "11111111-1111-1111-1111-111111111111",
              "AppContext": "transportados",
              "AllowedTenantIds": ["11111111-1111-1111-1111-111111111111"],
              "TenantMemberships": [
                {
                  "TenantMemberId": "33333333-3333-3333-3333-333333333333",
                  "TenantId": "11111111-1111-1111-1111-111111111111",
                  "TenantName": "Fake Transportados",
                  "Role": "Admin"
                }
              ],
              "ActiveTenantFeatures": {
                "Appearance": true,
                "Email": true
              }
            }
            """);

        Log.Warn(FakeBiometricsLogTag, "Seeded fake biometric restore state");
    }
#endif
}
