using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Transportados.Domain.Api.Domain;
using Transportados.Client.Models.Auth;
using Transportados.Client.Services.Auth;
using Transportados.Client.Services.Biometrics;
using Transportados.Client.Services.Storage;

namespace Transportados.Web.Test;

public sealed class AuthServiceTests : TestContext
{
    [Fact]
    public async Task TryRestoreSessionAsync_ForcesRefresh_AndPersistsRefreshedContext()
    {
        var oldToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(oldToken, CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));

        var service = CreateAuthService(storage, notifier, handler);

        var restored = await service.TryRestoreSessionAsync();

        Assert.True(restored);
        Assert.Equal(refreshedToken, await storage.GetAsync("accessToken"));
        Assert.Equal(1, notifier.AuthenticationNotifications);
        Assert.Equal(0, notifier.LogoutNotifications);
        Assert.Equal(["auth/refresh-token", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenRefreshFails_LogsOutAndReturnsFalse()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var service = CreateAuthService(storage, notifier, handler);
        var navigation = Services.GetRequiredService<NavigationManager>() as FakeNavigationManager;

        var restored = await service.TryRestoreSessionAsync();

        Assert.False(restored);
        Assert.Null(await storage.GetAsync("accessToken"));
        Assert.Equal(bool.FalseString, await storage.GetAsync("isAuthenticated"));
        Assert.Equal(0, notifier.AuthenticationNotifications);
        Assert.Equal(1, notifier.LogoutNotifications);
        Assert.NotNull(navigation);
        Assert.EndsWith("/login", navigation!.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenRefreshFailsSilently_ClearsSessionWithoutRenavigating()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var service = CreateAuthService(storage, notifier, handler);
        var navigation = Services.GetRequiredService<NavigationManager>() as FakeNavigationManager;
        navigation?.NavigateTo("/login");
        var currentUri = navigation?.Uri;

        var restored = await service.TryRestoreSessionAsync(notifyLogoutOnFailure: false);

        Assert.False(restored);
        Assert.Null(await storage.GetAsync("accessToken"));
        Assert.Equal(bool.FalseString, await storage.GetAsync("isAuthenticated"));
        Assert.Equal(0, notifier.AuthenticationNotifications);
        Assert.Equal(0, notifier.LogoutNotifications);
        Assert.NotNull(navigation);
        Assert.Equal(currentUri, navigation!.Uri);
    }

    [Fact]
    public async Task LoginWithStoredBiometricSessionAsync_WhenBiometricIsCanceled_DoesNotRefresh()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12))),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            new BiometricAuthenticationResult(BiometricAuthenticationStatus.Canceled));

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (success, errorMessage) = await service.LoginWithStoredBiometricSessionAsync();

        Assert.False(success);
        Assert.Contains("cancelada", errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, biometricService.AvailabilityChecks);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Empty(handler.RequestPaths);
        Assert.Equal(0, notifier.AuthenticationNotifications);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenBiometricsAvailableButNotEnabled_ContinuesWithoutBiometricAuthentication()
    {
        var oldToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(oldToken, CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            new BiometricAuthenticationResult(BiometricAuthenticationStatus.Canceled));

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var restored = await service.TryRestoreSessionAsync();

        Assert.True(restored);
        Assert.Equal(0, biometricService.AvailabilityChecks);
        Assert.Equal(0, biometricService.AuthenticationRequests);
        Assert.Equal(refreshedToken, await storage.GetAsync("accessToken"));
        Assert.Equal(["auth/refresh-token", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenBiometricsEnabledButNotConfirmed_DoesNotPromptOrRefresh()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12))),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var restored = await service.TryRestoreSessionAsync();

        Assert.False(restored);
        Assert.Equal(0, biometricService.AvailabilityChecks);
        Assert.Equal(0, biometricService.AuthenticationRequests);
        Assert.Empty(handler.RequestPaths);
        Assert.Equal(bool.TrueString, await storage.GetAsync("biometricAuthenticationEnabled"));
        Assert.NotNull(await storage.GetAsync("accessToken"));
        Assert.Equal(0, notifier.AuthenticationNotifications);
        Assert.Equal(0, notifier.LogoutNotifications);
    }

    [Fact]
    public async Task CanUseBiometricLoginAsync_WhenStoredSessionEnabledAndAvailable_ReturnsTrue()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler();
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);
        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var canUse = await service.CanUseBiometricLoginAsync();

        Assert.True(canUse);
        Assert.Equal(1, biometricService.AvailabilityChecks);
        Assert.Equal(0, biometricService.AuthenticationRequests);
        Assert.Empty(handler.RequestPaths);
    }

    [Fact]
    public async Task LoginWithStoredBiometricSessionAsync_WhenBiometricSucceeds_RestoresSession()
    {
        var oldToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(oldToken, CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (success, errorMessage) = await service.LoginWithStoredBiometricSessionAsync();

        Assert.True(success, errorMessage);
        Assert.Equal(refreshedToken, await storage.GetAsync("accessToken"));
        Assert.Equal(1, biometricService.AvailabilityChecks);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Equal(BiometricRequestReason.SessionRestore, biometricService.LastRequestReason);
        Assert.Equal(["auth/refresh-token", "auth/me"], handler.RequestPaths);
        Assert.Equal(1, notifier.AuthenticationNotifications);
    }

    [Fact]
    public async Task LoginAsync_WhenSuccessful_ClearsBiometricAuthenticationState()
    {
        var loginToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(4)), CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, loginToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var service = CreateAuthService(storage, notifier, handler);

        var (success, errorMessage) = await service.LoginAsync("new-user@transportados.local", "secret");

        Assert.True(success, errorMessage);
        Assert.Null(await storage.GetAsync("biometricAuthenticationEnabled"));
        Assert.Equal(loginToken, await storage.GetAsync("accessToken"));
        Assert.Equal(["auth/gettoken", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task EnableBiometricAuthenticationAsync_WhenPromptSucceeds_StoresFlagUntilDisabled()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler();
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);
        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (success, errorMessage) = await service.EnableBiometricAuthenticationAsync();

        Assert.True(success, errorMessage);
        Assert.Equal(bool.TrueString, await storage.GetAsync("biometricAuthenticationEnabled"));
        Assert.Equal(1, biometricService.AvailabilityChecks);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Equal(BiometricRequestReason.ProfileOptIn, biometricService.LastRequestReason);

        await service.DisableBiometricAuthenticationAsync();

        Assert.Null(await storage.GetAsync("biometricAuthenticationEnabled"));
    }

    [Fact]
    public async Task LoginWithStoredBiometricSessionAsync_WhenBiometricsUnavailable_DoesNotRefresh()
    {
        var oldToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(oldToken, CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.NotAvailable,
            new BiometricAuthenticationResult(BiometricAuthenticationStatus.NotAvailable));

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (success, errorMessage) = await service.LoginWithStoredBiometricSessionAsync();

        Assert.False(success);
        Assert.Contains("no esta disponible", errorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, biometricService.AvailabilityChecks);
        Assert.Equal(0, biometricService.AuthenticationRequests);
        Assert.Equal(oldToken, await storage.GetAsync("accessToken"));
        Assert.Empty(handler.RequestPaths);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenBiometricAlreadyConfirmedInSession_DoesNotPromptAgain()
    {
        var token = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken1 = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(10));
        var refreshedToken2 = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(token, CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken1),
            JsonResponse(HttpStatusCode.OK, CreateContext()),
            JsonResponse(HttpStatusCode.OK, refreshedToken2),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (enabled, enableError) = await service.EnableBiometricAuthenticationAsync();
        var firstRestore = await service.TryRestoreSessionAsync();
        var secondRestore = await service.TryRestoreSessionAsync();

        Assert.True(enabled, enableError);
        Assert.True(firstRestore);
        Assert.True(secondRestore);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Equal(["auth/refresh-token", "auth/me", "auth/refresh-token", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_WhenConcurrentRestoresAreAlreadyConfirmed_DoesNotPromptAgain()
    {
        var token = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken1 = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(10));
        var refreshedToken2 = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(token, CreateContext(), biometricAuthenticationEnabled: true);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken1),
            JsonResponse(HttpStatusCode.OK, CreateContext()),
            JsonResponse(HttpStatusCode.OK, refreshedToken2),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new SlowTestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded,
            TimeSpan.FromMilliseconds(100));

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (enabled, enableError) = await service.EnableBiometricAuthenticationAsync();
        var restoreTasks = new[]
        {
            service.TryRestoreSessionAsync(),
            service.TryRestoreSessionAsync()
        };
        var results = await Task.WhenAll(restoreTasks);

        Assert.True(enabled, enableError);
        Assert.All(results, Assert.True);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Equal(["auth/refresh-token", "auth/me", "auth/refresh-token", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task EnableBiometricAuthenticationAsync_WhenFollowedByRouteRestore_DoesNotPromptAgain()
    {
        var storedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(storedToken, CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);

        var service = CreateAuthService(storage, notifier, handler, biometricService);

        var (success, errorMessage) = await service.EnableBiometricAuthenticationAsync();
        var restored = await service.TryRestoreSessionAsync();

        Assert.True(success, errorMessage);
        Assert.True(restored);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Equal(["auth/refresh-token", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task EnableBiometricAuthenticationAsync_WhenRouteRestoreUsesDifferentAuthService_DoesNotPromptAgain()
    {
        var storedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var refreshedToken = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(12));
        var storage = new TestStorage(storedToken, CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, refreshedToken),
            JsonResponse(HttpStatusCode.OK, CreateContext()));
        var biometricService = new TestBiometricAuthenticationService(
            BiometricAvailability.Available,
            BiometricAuthenticationResult.Succeeded);
        var httpClientFactory = new StaticHttpClientFactory(handler);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var biometricSessionState = new BiometricAuthenticationSessionState();

        var loginService = new AuthService(
            httpClientFactory,
            storage,
            notifier,
            navigation,
            biometricService,
            biometricSessionState,
            NullLogger<AuthService>.Instance);
        var routeService = new AuthService(
            httpClientFactory,
            storage,
            notifier,
            navigation,
            biometricService,
            biometricSessionState,
            NullLogger<AuthService>.Instance);

        var (success, errorMessage) = await loginService.EnableBiometricAuthenticationAsync();
        var restored = await routeService.TryRestoreSessionAsync();

        Assert.True(success, errorMessage);
        Assert.True(restored);
        Assert.Equal(1, biometricService.AuthenticationRequests);
        Assert.Equal(["auth/refresh-token", "auth/me"], handler.RequestPaths);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_WhenRefreshFails_DoesNotReturnStaleToken()
    {
        var expiringToken = CreateJwtToken(DateTimeOffset.UtcNow.AddMinutes(1));
        var storage = new TestStorage(expiringToken, CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var service = CreateAuthService(storage, notifier, handler);

        var token = await service.GetValidAccessTokenAsync();

        Assert.Null(token);
        Assert.Null(await storage.GetAsync("accessToken"));
        Assert.Equal(1, notifier.LogoutNotifications);
    }

    [Fact]
    public async Task LoginAsync_WithoutActiveRole_OmitsRoleAndTenantFromRequest()
    {
        var token = CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8));
        var context = CreateContext();
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddMinutes(1)), context);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(
            JsonResponse(HttpStatusCode.OK, token),
            JsonResponse(HttpStatusCode.OK, context));

        var service = CreateAuthService(storage, notifier, handler);

        var (success, errorMessage) = await service.LoginAsync("admin@transportados.local", "secret");

        Assert.True(success, errorMessage);
        Assert.Equal(token, await storage.GetAsync("accessToken"));
        Assert.Equal(["auth/gettoken", "auth/me"], handler.RequestPaths);
        Assert.Single(handler.RequestBodies);

        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = payload.RootElement;
        Assert.Equal("admin@transportados.local", root.GetProperty("username").GetString());
        Assert.Equal("secret", root.GetProperty("password").GetString());
        Assert.Equal("transportados", root.GetProperty("appContext").GetString());
        Assert.False(root.TryGetProperty("activeRole", out _));
        Assert.False(root.TryGetProperty("activeTenantId", out _));
    }

    [Fact]
    public async Task RequestPasswordRecoveryCodeAsync_PostsPublicAuthEndpoint()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateAuthService(storage, notifier, handler);

        var (success, errorMessage) = await service.RequestPasswordRecoveryCodeAsync(
            "admin@transportados.local",
            "https://transportados.example.com/");

        Assert.True(success, errorMessage);
        Assert.Equal(["auth/getvercode"], handler.RequestPaths);
        Assert.Single(handler.RequestBodies);
        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = payload.RootElement;
        Assert.Equal("admin@transportados.local", root.GetProperty("email").GetString());
        Assert.Equal("https://transportados.example.com/", root.GetProperty("publicOrigin").GetString());
    }

    [Fact]
    public async Task ChangePasswordWithCodeAsync_PostsPublicAuthEndpoint()
    {
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8)), CreateContext());
        var notifier = new AuthStateNotifierSpy();
        var handler = new SequenceHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var service = CreateAuthService(storage, notifier, handler);

        var (success, errorMessage) = await service.ChangePasswordWithCodeAsync(
            "admin@transportados.local",
            "123456",
            "new-password");

        Assert.True(success, errorMessage);
        Assert.Equal(["auth/changepasswordwithcode"], handler.RequestPaths);
        Assert.Single(handler.RequestBodies);
        using var payload = JsonDocument.Parse(handler.RequestBodies[0]);
        var root = payload.RootElement;
        Assert.Equal("admin@transportados.local", root.GetProperty("email").GetString());
        Assert.Equal("123456", root.GetProperty("verificationCode").GetString());
        Assert.Equal("new-password", root.GetProperty("newPassword").GetString());
    }

    private AuthService CreateAuthService(
        IClientStorageService storage,
        IAuthStateNotifier notifier,
        HttpMessageHandler handler,
        IBiometricAuthenticationService? biometricAuthenticationService = null)
    {
        Services.AddSingleton(storage);
        Services.AddSingleton(notifier);
        Services.AddSingleton<IHttpClientFactory>(new StaticHttpClientFactory(handler));

        return new AuthService(
            Services.GetRequiredService<IHttpClientFactory>(),
            Services.GetRequiredService<IClientStorageService>(),
            Services.GetRequiredService<IAuthStateNotifier>(),
            Services.GetRequiredService<NavigationManager>(),
            biometricAuthenticationService ?? new NoOpBiometricAuthenticationService(),
            new BiometricAuthenticationSessionState(),
            NullLogger<AuthService>.Instance);
    }

    private static AuthenticatedContext CreateContext()
    {
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return new AuthenticatedContext
        {
            UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Email = "admin@transportados.local",
            FullName = "Admin Transportados",
            ActiveRole = Roles.Admin,
            ActiveTenantId = tenantId,
            AppContext = "transportados",
            AllowedTenantIds = [tenantId],
            TenantMemberships =
            [
                new TenantMemberInfo
                {
                    TenantMemberId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    TenantId = tenantId,
                    TenantName = "Transportados",
                    Role = Roles.Admin
                }
            ]
        };
    }

    private static string CreateJwtToken(DateTimeOffset expiresAtUtc)
    {
        var header = Base64UrlEncode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncode($"{{\"exp\":{expiresAtUtc.ToUnixTimeSeconds()}}}");
        return $"{header}.{payload}.signature";
    }

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload) =>
        new(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    private sealed class StaticHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _httpClient = new(handler)
        {
            BaseAddress = new Uri("https://transportados-api.local/")
        };

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class SequenceHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> RequestPaths { get; } = [];
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri?.AbsolutePath.Trim('/') ?? string.Empty);
            if (request.Content != null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP responses left in the test sequence.");
            }

            return _responses.Dequeue();
        }
    }

    private sealed class TestStorage(
        string accessToken,
        AuthenticatedContext context,
        bool biometricAuthenticationEnabled = false) : IClientStorageService
    {
        private readonly Dictionary<string, string> _values = CreateInitialValues(accessToken, context, biometricAuthenticationEnabled);

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public Task RemoveAsync(string key)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class AuthStateNotifierSpy : IAuthStateNotifier
    {
        public int AuthenticationNotifications { get; private set; }
        public int LogoutNotifications { get; private set; }

        public void NotifyUserAuthentication(AuthenticatedContext userInfo)
        {
            AuthenticationNotifications++;
        }

        public void NotifyUserLogout()
        {
            LogoutNotifications++;
        }
    }

    private sealed class TestBiometricAuthenticationService(
        BiometricAvailability availability,
        BiometricAuthenticationResult authenticationResult) : IBiometricAuthenticationService
    {
        public int AvailabilityChecks { get; private set; }

        public int AuthenticationRequests { get; private set; }

        public BiometricRequestReason? LastRequestReason { get; private set; }

        public Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            AvailabilityChecks++;
            return Task.FromResult(availability);
        }

        public Task<BiometricAuthenticationResult> AuthenticateAsync(
            BiometricAuthenticationRequest request,
            CancellationToken cancellationToken = default)
        {
            AuthenticationRequests++;
            LastRequestReason = request.Reason;
            return Task.FromResult(authenticationResult);
        }
    }

    private sealed class SlowTestBiometricAuthenticationService(
        BiometricAvailability availability,
        BiometricAuthenticationResult authenticationResult,
        TimeSpan authenticationDelay) : IBiometricAuthenticationService
    {
        public int AuthenticationRequests { get; private set; }

        public Task<BiometricAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(availability);

        public async Task<BiometricAuthenticationResult> AuthenticateAsync(
            BiometricAuthenticationRequest request,
            CancellationToken cancellationToken = default)
        {
            AuthenticationRequests++;
            await Task.Delay(authenticationDelay, cancellationToken);
            return authenticationResult;
        }
    }

    private static Dictionary<string, string> CreateInitialValues(
        string accessToken,
        AuthenticatedContext context,
        bool biometricAuthenticationEnabled)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accessToken"] = accessToken,
            ["isAuthenticated"] = "true",
            ["userId"] = context.UserId.ToString("D"),
            ["userEmail"] = context.Email,
            ["activeRole"] = context.ActiveRole ?? string.Empty,
            ["activeTenantId"] = context.ActiveTenantId?.ToString("D") ?? string.Empty,
            ["appContext"] = context.AppContext ?? "transportados",
            ["authContext"] = JsonSerializer.Serialize(context)
        };

        if (biometricAuthenticationEnabled)
        {
            values["biometricAuthenticationEnabled"] = bool.TrueString;
        }

        return values;
    }
}
