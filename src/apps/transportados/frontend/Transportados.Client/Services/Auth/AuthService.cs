using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Client.Models.Auth;
using Transportados.Client.Services.Biometrics;
using Transportados.Client.Services.Storage;

namespace Transportados.Client.Services.Auth;

public sealed class AuthService(
    IHttpClientFactory httpClientFactory,
    IClientStorageService storage,
    IAuthStateNotifier authStateNotifier,
    NavigationManager navigation,
    IBiometricAuthenticationService biometricAuthenticationService,
    BiometricAuthenticationSessionState biometricAuthenticationSessionState,
    ILogger<AuthService> logger)
{
    private const string AccessTokenKey = "accessToken";
    private const string IsAuthenticatedKey = "isAuthenticated";
    private const string UserIdKey = "userId";
    private const string UserEmailKey = "userEmail";
    private const string ActiveRoleKey = "activeRole";
    private const string ActiveTenantIdKey = "activeTenantId";
    private const string AppContextKey = "appContext";
    private const string AuthContextKey = "authContext";
    private const string BiometricAuthenticationEnabledKey = "biometricAuthenticationEnabled";
    private static readonly TimeSpan RefreshBeforeExpiration = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<(bool Success, string? ErrorMessage)> LoginAsync(string username, string password, string? activeRole = null, Guid? activeTenantId = null)
    {
        try
        {
            var client = httpClientFactory.CreateClient("TransportadosApi");
            var request = new LoginRequest
            {
                Username = username,
                Password = password,
                ActiveRole = activeRole,
                ActiveTenantId = activeTenantId,
                AppContext = "transportados"
            };

            using var response = await client.PostAsJsonAsync("auth/gettoken", request);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return (false, "Credenciales invalidas.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (false, $"Login fallo ({(int)response.StatusCode}): {errorBody}");
            }

            var token = await response.Content.ReadFromJsonAsync<string>();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "El backend no devolvio token de acceso.");
            }

            await PersistTokenAsync(token);

            var context = await FetchAuthenticatedContextAsync(token);
            if (context is null)
            {
                await LogoutInternalAsync(notify: false);
                return (false, "No se pudo recuperar el contexto de usuario.");
            }

            await PersistContextAsync(context);
            await ClearBiometricAuthenticationStateAsync();
            NotifyAuthenticated(context);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login request failed for {Username}", username);
            return (false, ex.Message);
        }
    }

    public async Task<BiometricAvailability> GetBiometricAvailabilityAsync()
    {
        try
        {
            return await biometricAuthenticationService.GetAvailabilityAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Biometric availability check failed.");
            return BiometricAvailability.Unknown;
        }
    }

    public async Task<bool> CanUseBiometricLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(await storage.GetAsync(AccessTokenKey)) ||
            !await IsBiometricAuthenticationEnabledAsync())
        {
            return false;
        }

        return await GetBiometricAvailabilityAsync() == BiometricAvailability.Available;
    }

    public async Task<(bool Success, string? ErrorMessage)> LoginWithStoredBiometricSessionAsync()
    {
        var token = await storage.GetAsync(AccessTokenKey);
        if (string.IsNullOrWhiteSpace(token) || !await IsBiometricAuthenticationEnabledAsync())
        {
            return (false, "No hay una sesion biometrica guardada en este dispositivo.");
        }

        var availability = await GetBiometricAvailabilityAsync();
        if (availability != BiometricAvailability.Available)
        {
            return (false, ResolveBiometricAvailabilityMessage(availability));
        }

        var biometricResult = await biometricAuthenticationService.AuthenticateAsync(
            BiometricAuthenticationRequest.ForSessionRestore());
        if (biometricResult.Status != BiometricAuthenticationStatus.Succeeded)
        {
            return (false, ResolveBiometricAuthenticationMessage(biometricResult));
        }

        biometricAuthenticationSessionState.MarkConfirmed();

        var refreshedToken = await TryRefreshTokenAsync(forceRefresh: true);
        return string.IsNullOrWhiteSpace(refreshedToken)
            ? (false, "La sesion expiro. Ingresa con usuario y clave.")
            : (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> EnableBiometricAuthenticationAsync()
    {
        if (string.IsNullOrWhiteSpace(await storage.GetAsync(AccessTokenKey)))
        {
            return (false, "La sesion expiro. Ingresa nuevamente para activar biometria.");
        }

        var availability = await GetBiometricAvailabilityAsync();
        if (availability != BiometricAvailability.Available)
        {
            return (false, ResolveBiometricAvailabilityMessage(availability));
        }

        var biometricResult = await biometricAuthenticationService.AuthenticateAsync(
            BiometricAuthenticationRequest.ForProfileOptIn());
        if (biometricResult.Status != BiometricAuthenticationStatus.Succeeded)
        {
            return (false, ResolveBiometricAuthenticationMessage(biometricResult));
        }

        await storage.SetAsync(BiometricAuthenticationEnabledKey, bool.TrueString);
        biometricAuthenticationSessionState.MarkConfirmed();
        return (true, null);
    }

    public async Task DisableBiometricAuthenticationAsync()
    {
        await ClearBiometricAuthenticationStateAsync();
    }

    public async Task<(bool Success, string? ErrorMessage)> RequestPasswordRecoveryCodeAsync(string email, string? publicOrigin)
    {
        try
        {
            var client = httpClientFactory.CreateClient("TransportadosApi");
            using var response = await client.PostAsJsonAsync("auth/getvercode", new PasswordRecoveryCodeRequestDto
            {
                Email = email,
                PublicOrigin = publicOrigin
            });

            return await ReadPublicCommandResponse(response, "No se pudo enviar el codigo de recuperacion.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password recovery code request failed.");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordWithCodeAsync(
        string email,
        string verificationCode,
        string newPassword)
    {
        try
        {
            var client = httpClientFactory.CreateClient("TransportadosApi");
            using var response = await client.PostAsJsonAsync("auth/changepasswordwithcode", new ChangePasswordWithCodeRequestDto
            {
                Email = email,
                VerificationCode = verificationCode,
                NewPassword = newPassword
            });

            return await ReadPublicCommandResponse(response, "No se pudo cambiar la clave.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Password recovery change request failed.");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SelectContextAsync(TenantMemberInfo membership)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "La sesion expiro.");
            }

            var client = httpClientFactory.CreateClient("TransportadosApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.PostAsJsonAsync("auth/select-context", new SelectContextRequest
            {
                ActiveRole = membership.Role,
                ActiveTenantId = membership.TenantId,
                AppContext = "transportados"
            });

            if (!response.IsSuccessStatusCode)
            {
                return (false, await response.Content.ReadAsStringAsync());
            }

            var newToken = await response.Content.ReadFromJsonAsync<string>();
            if (string.IsNullOrWhiteSpace(newToken))
            {
                return (false, "El backend no devolvio token de acceso.");
            }

            await PersistTokenAsync(newToken);
            var context = await FetchAuthenticatedContextAsync(newToken);
            if (context == null)
            {
                return (false, "No se pudo recuperar el contexto activo.");
            }

            await PersistContextAsync(context);
            NotifyAuthenticated(context);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Context switch failed");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> SelectPlatformContextAsync()
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "La sesion expiro.");
            }

            var client = httpClientFactory.CreateClient("TransportadosApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.PostAsJsonAsync("auth/select-context", new SelectContextRequest
            {
                ActiveRole = Roles.SuperAdmin,
                ActiveTenantId = null,
                AppContext = "transportados"
            });

            if (!response.IsSuccessStatusCode)
            {
                return (false, await response.Content.ReadAsStringAsync());
            }

            var newToken = await response.Content.ReadFromJsonAsync<string>();
            if (string.IsNullOrWhiteSpace(newToken))
            {
                return (false, "El backend no devolvio token de acceso.");
            }

            await PersistTokenAsync(newToken);
            var context = await FetchAuthenticatedContextAsync(newToken);
            if (context == null)
            {
                return (false, "No se pudo recuperar el contexto activo.");
            }

            await PersistContextAsync(context);
            NotifyAuthenticated(context);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Platform context switch failed");
            return (false, ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        await LogoutInternalAsync(notify: true);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var isAuthenticatedRaw = await storage.GetAsync(IsAuthenticatedKey);
        return bool.TryParse(isAuthenticatedRaw, out var parsed) && parsed;
    }

    public async Task<bool> TryRestoreSessionAsync(bool notifyLogoutOnFailure = true)
    {
        var token = await storage.GetAsync(AccessTokenKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (await IsBiometricAuthenticationEnabledAsync() && !biometricAuthenticationSessionState.IsConfirmed)
        {
            return false;
        }

        var refreshedToken = await TryRefreshTokenAsync(forceRefresh: true, notifyLogoutOnFailure);
        return !string.IsNullOrWhiteSpace(refreshedToken);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        return await storage.GetAsync(AccessTokenKey);
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!IsTokenExpiring(token, RefreshBeforeExpiration))
        {
            return token;
        }

        return await TryRefreshTokenAsync();
    }

    public async Task<string?> TryRefreshTokenAsync()
    {
        return await TryRefreshTokenAsync(forceRefresh: false);
    }

    public async Task<string?> TryRefreshTokenAsync(bool forceRefresh)
    {
        return await TryRefreshTokenAsync(forceRefresh, notifyLogoutOnFailure: true);
    }

    private async Task<string?> TryRefreshTokenAsync(bool forceRefresh, bool notifyLogoutOnFailure)
    {
        await _refreshLock.WaitAsync();
        try
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            if (!forceRefresh && !IsTokenExpiring(token, RefreshBeforeExpiration))
            {
                return token;
            }

            var client = httpClientFactory.CreateClient("TransportadosApi");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.PostAsync("auth/refresh-token", content: null);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Token refresh failed with status {StatusCode}", response.StatusCode);
                await LogoutInternalAsync(notify: notifyLogoutOnFailure);
                return null;
            }

            var refreshedToken = await response.Content.ReadFromJsonAsync<string>();
            if (string.IsNullOrWhiteSpace(refreshedToken))
            {
                logger.LogWarning("Token refresh returned an empty token payload.");
                await LogoutInternalAsync(notify: notifyLogoutOnFailure);
                return null;
            }

            await PersistTokenAsync(refreshedToken);

            var context = await FetchAuthenticatedContextAsync(refreshedToken);
            if (context is null)
            {
                logger.LogWarning("Token refreshed but auth context fetch failed.");
                await LogoutInternalAsync(notify: notifyLogoutOnFailure);
                return null;
            }

            await PersistContextAsync(context);
            NotifyAuthenticated(context);
            return refreshedToken;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token refresh failed due to an unexpected error.");
            await LogoutInternalAsync(notify: notifyLogoutOnFailure);
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<AuthenticatedContext?> GetStoredContextAsync()
    {
        var rawContext = await storage.GetAsync(AuthContextKey);
        if (string.IsNullOrWhiteSpace(rawContext))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AuthenticatedContext>(rawContext);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(bool Success, string? ErrorMessage)> ReadPublicCommandResponse(
        HttpResponseMessage response,
        string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var body = response.Content == null ? null : await response.Content.ReadAsStringAsync();
        return (false, NormalizeErrorBody(body, fallbackMessage));
    }

    private static string NormalizeErrorBody(string? body, string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallbackMessage;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString() ?? fallbackMessage;
            }

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString() ?? fallbackMessage;
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString() ?? fallbackMessage;
            }
        }
        catch (JsonException)
        {
            return body.Trim();
        }

        return fallbackMessage;
    }

    private static string ResolveBiometricAvailabilityMessage(BiometricAvailability availability) =>
        availability switch
        {
            BiometricAvailability.NotEnrolled => "No hay biometria configurada en este dispositivo.",
            BiometricAvailability.NotAvailable => "La biometria no esta disponible en este dispositivo.",
            BiometricAvailability.TemporarilyUnavailable => "La biometria esta temporalmente no disponible.",
            _ => "No se pudo validar disponibilidad biometrica."
        };

    private static string ResolveBiometricAuthenticationMessage(BiometricAuthenticationResult result) =>
        result.Status switch
        {
            BiometricAuthenticationStatus.Canceled => "Autenticacion biometrica cancelada.",
            BiometricAuthenticationStatus.NotEnrolled => "No hay biometria configurada en este dispositivo.",
            BiometricAuthenticationStatus.NotAvailable => "La biometria no esta disponible en este dispositivo.",
            BiometricAuthenticationStatus.LockedOut => "La biometria esta bloqueada temporalmente.",
            BiometricAuthenticationStatus.Failed => "No se pudo validar la biometria.",
            _ => result.ErrorMessage ?? "No se pudo validar la biometria."
        };

    public async Task<bool> IsBiometricAuthenticationEnabledAsync()
    {
        var rawValue = await storage.GetAsync(BiometricAuthenticationEnabledKey);
        return bool.TryParse(rawValue, out var enabled) && enabled;
    }

    private async Task LogoutInternalAsync(bool notify)
    {
        await ClearBiometricAuthenticationStateAsync();
        await storage.RemoveAsync(AccessTokenKey);
        await storage.RemoveAsync(UserIdKey);
        await storage.RemoveAsync(UserEmailKey);
        await storage.RemoveAsync(ActiveRoleKey);
        await storage.RemoveAsync(ActiveTenantIdKey);
        await storage.RemoveAsync(AppContextKey);
        await storage.RemoveAsync(AuthContextKey);
        await storage.SetAsync(IsAuthenticatedKey, false.ToString());

        if (notify)
        {
            authStateNotifier.NotifyUserLogout();

            navigation.NavigateTo("/login", replace: true);
        }
    }

    private async Task ClearBiometricAuthenticationStateAsync()
    {
        biometricAuthenticationSessionState.Reset();
        await storage.RemoveAsync(BiometricAuthenticationEnabledKey);
    }

    private async Task PersistTokenAsync(string token)
    {
        await storage.SetAsync(AccessTokenKey, token);
        await storage.SetAsync(IsAuthenticatedKey, true.ToString());
    }

    private async Task PersistContextAsync(AuthenticatedContext context)
    {
        await storage.SetAsync(UserIdKey, context.UserId.ToString("D"));
        await storage.SetAsync(UserEmailKey, context.Email);
        await storage.SetAsync(ActiveRoleKey, context.ActiveRole ?? string.Empty);
        await storage.SetAsync(ActiveTenantIdKey, context.ActiveTenantId?.ToString("D") ?? string.Empty);
        await storage.SetAsync(AppContextKey, context.AppContext ?? string.Empty);
        await storage.SetAsync(AuthContextKey, JsonSerializer.Serialize(context));
    }

    private void NotifyAuthenticated(AuthenticatedContext context)
    {
        authStateNotifier.NotifyUserAuthentication(context);
    }

    private async Task<AuthenticatedContext?> FetchAuthenticatedContextAsync(string token)
    {
        var client = httpClientFactory.CreateClient("TransportadosApi");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var response = await client.GetAsync("auth/me");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch auth context. Status {StatusCode}", response.StatusCode);
                return null;
            }

            var context = await response.Content.ReadFromJsonAsync<AuthenticatedContext>();
            return context;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error fetching /auth/me context");
            return null;
        }
    }

    private static bool IsTokenExpiring(string token, TimeSpan threshold)
    {
        try
        {
            var segments = token.Split('.');
            if (segments.Length < 2)
            {
                return true;
            }

            var payloadBytes = DecodeBase64Url(segments[1]);
            using var payload = JsonDocument.Parse(payloadBytes);
            if (!payload.RootElement.TryGetProperty("exp", out var expElement))
            {
                return false;
            }

            var expSeconds = expElement.GetInt64();
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            return expiresAt <= DateTime.UtcNow.Add(threshold);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static byte[] DecodeBase64Url(string base64Url)
    {
        var normalized = base64Url.Replace('-', '+').Replace('_', '/');
        return (normalized.Length % 4) switch
        {
            2 => Convert.FromBase64String(normalized + "=="),
            3 => Convert.FromBase64String(normalized + "="),
            _ => Convert.FromBase64String(normalized)
        };
    }
}
