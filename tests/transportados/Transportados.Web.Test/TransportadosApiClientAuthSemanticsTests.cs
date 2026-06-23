using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Client.Models.Auth;
using Transportados.Client.Services.Api;
using Transportados.Client.Services.Auth;
using Transportados.Client.Services.Biometrics;
using Transportados.Client.Services.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Transportados.Web.Test;

public sealed class TransportadosApiClientAuthSemanticsTests : TestContext
{
    [Fact]
    public async Task GetAsync_WhenUnauthorized_RefreshesOnceAndRetries()
    {
        var context = CreateContext();
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(4)), context);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SemanticsHttpMessageHandler(context)
        {
            FirstCustomerResponseCode = HttpStatusCode.Unauthorized
        };

        var (apiClient, _) = CreateClient(storage, notifier, handler);

        var response = await apiClient.GetAsync<CustomerListResponseDto>("api/customers");

        Assert.NotNull(response);
        Assert.Equal(1, response.Total);
        Assert.Equal(2, handler.CustomerRequestCount);
        Assert.Equal(1, handler.RefreshTokenRequestCount);
        Assert.Equal(0, notifier.LogoutNotifications);
    }

    [Fact]
    public async Task GetAsync_WhenContextInvalidForbidden_LogsOutAndRedirects()
    {
        var context = CreateContext();
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(4)), context);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SemanticsHttpMessageHandler(context)
        {
            FirstCustomerResponseCode = HttpStatusCode.Forbidden,
            ForbiddenCode = ApiErrorCodes.ContextInvalid,
            ForbiddenDetail = "The active context is no longer valid."
        };

        var (apiClient, navigation) = CreateClient(storage, notifier, handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => apiClient.GetAsync<CustomerListResponseDto>("api/customers"));

        Assert.Contains("context", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, notifier.LogoutNotifications);
        Assert.Null(await storage.GetAsync("accessToken"));
        Assert.Equal(bool.FalseString, await storage.GetAsync("isAuthenticated"));
        Assert.EndsWith("/login", navigation.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAsync_WhenValidEmptyList_ReturnsEmptyWithoutLogout()
    {
        var context = CreateContext();
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(4)), context);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SemanticsHttpMessageHandler(context)
        {
            FirstCustomerResponseCode = HttpStatusCode.OK,
            CustomerPayload = new CustomerListResponseDto
            {
                Total = 0,
                Customers = []
            }
        };

        var (apiClient, _) = CreateClient(storage, notifier, handler);

        var response = await apiClient.GetAsync<CustomerListResponseDto>("api/customers");

        Assert.NotNull(response);
        Assert.Equal(0, response.Total);
        Assert.Empty(response.Customers);
        Assert.Equal(0, notifier.LogoutNotifications);
    }

    [Fact]
    public async Task PostAsync_WhenNoContentResponse_ReturnsDefaultWithoutThrowing()
    {
        var context = CreateContext();
        var storage = new TestStorage(CreateJwtToken(DateTimeOffset.UtcNow.AddHours(4)), context);
        var notifier = new AuthStateNotifierSpy();
        var handler = new SemanticsHttpMessageHandler(context);
        var (apiClient, _) = CreateClient(storage, notifier, handler);

        var result = await apiClient.PostAsync<TenantUpdateRequestDto, object?>(
            $"api/platform/tenants/{Guid.NewGuid():D}",
            new TenantUpdateRequestDto { Status = TenantStatus.Active });

        Assert.Null(result);
        Assert.Equal(1, handler.TenantUpdatePostCount);
        Assert.Equal(0, notifier.LogoutNotifications);
    }

    private (TransportadosApiClient ApiClient, FakeNavigationManager Navigation) CreateClient(
        IClientStorageService storage,
        IAuthStateNotifier notifier,
        HttpMessageHandler handler)
    {
        var navigation = Services.GetRequiredService<NavigationManager>() as FakeNavigationManager
            ?? throw new InvalidOperationException("FakeNavigationManager is not available in test context.");
        var httpClientFactory = new StaticHttpClientFactory(handler);
        var authService = new AuthService(
            httpClientFactory,
            storage,
            notifier,
            navigation,
            new NoOpBiometricAuthenticationService(),
            new BiometricAuthenticationSessionState(),
            NullLogger<AuthService>.Instance);
        return (new TransportadosApiClient(httpClientFactory, authService), navigation);
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
        private readonly HttpClient _client = new(handler)
        {
            BaseAddress = new Uri("https://transportados-api.local/")
        };

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class SemanticsHttpMessageHandler(AuthenticatedContext context) : HttpMessageHandler
    {
        public int CustomerRequestCount { get; private set; }
        public int RefreshTokenRequestCount { get; private set; }
        public int TenantUpdatePostCount { get; private set; }
        public HttpStatusCode FirstCustomerResponseCode { get; set; } = HttpStatusCode.OK;
        public string ForbiddenCode { get; set; } = ApiErrorCodes.ContextInvalid;
        public string ForbiddenDetail { get; set; } = "Forbidden.";
        public CustomerListResponseDto CustomerPayload { get; set; } = new()
        {
            Total = 1,
            Customers = [new CustomerDto { Id = Guid.NewGuid(), TenantId = context.ActiveTenantId ?? Guid.NewGuid(), FullName = "Cliente Demo" }]
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath.Trim('/');
            if (request.Method == HttpMethod.Post &&
                !string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("api/platform/tenants/", StringComparison.OrdinalIgnoreCase))
            {
                TenantUpdatePostCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            if (string.Equals(path, "api/customers", StringComparison.OrdinalIgnoreCase))
            {
                CustomerRequestCount++;
                if (CustomerRequestCount == 1)
                {
                    return Task.FromResult(FirstCustomerResponseCode switch
                    {
                        HttpStatusCode.Unauthorized => new HttpResponseMessage(HttpStatusCode.Unauthorized),
                        HttpStatusCode.Forbidden => JsonResponse(HttpStatusCode.Forbidden, new
                        {
                            title = "Forbidden",
                            status = 403,
                            detail = ForbiddenDetail,
                            code = ForbiddenCode
                        }),
                        _ => JsonResponse(HttpStatusCode.OK, CustomerPayload)
                    });
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, CustomerPayload));
            }

            if (string.Equals(path, "auth/refresh-token", StringComparison.OrdinalIgnoreCase))
            {
                RefreshTokenRequestCount++;
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, CreateJwtToken(DateTimeOffset.UtcNow.AddHours(8))));
            }

            if (string.Equals(path, "auth/me", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, context));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class TestStorage(string accessToken, AuthenticatedContext context) : IClientStorageService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
        {
            ["accessToken"] = accessToken,
            ["isAuthenticated"] = bool.TrueString,
            ["userId"] = context.UserId.ToString("D"),
            ["userEmail"] = context.Email,
            ["activeRole"] = context.ActiveRole ?? string.Empty,
            ["activeTenantId"] = context.ActiveTenantId?.ToString("D") ?? string.Empty,
            ["appContext"] = context.AppContext ?? "transportados",
            ["authContext"] = JsonSerializer.Serialize(context)
        };

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
}
