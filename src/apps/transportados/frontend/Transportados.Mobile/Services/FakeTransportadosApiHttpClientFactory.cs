#if USE_FAKE_BIOMETRICS
using System.Net;
using System.Text;
using Android.Util;

namespace Transportados.Mobile.Services;

public sealed class FakeTransportadosApiHttpClientFactory : IHttpClientFactory
{
    private const string LogTag = "TRANSPORTADOS_FAKE_BIOMETRICS";

    public HttpClient CreateClient(string name)
    {
        Log.Warn(LogTag, $"Fake API client requested: {name}");
        return new HttpClient(new FakeTransportadosApiHttpMessageHandler())
        {
            BaseAddress = new Uri("https://transportados-api.fake/")
        };
    }

    private sealed class FakeTransportadosApiHttpMessageHandler : HttpMessageHandler
    {
        private static int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _requestCount);
            var path = request.RequestUri?.AbsolutePath.Trim('/') ?? string.Empty;
            Log.Warn(LogTag, $"Fake API request #{count}: {request.Method} {path}");

            if (string.Equals(path, "auth/gettoken", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, "auth/refresh-token", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(FakeToken()));
            }

            if (string.Equals(path, "auth/me", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(FakeContextJson));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"Fake endpoint not found: {path}", Encoding.UTF8, "text/plain")
            });
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

        private static string FakeToken()
        {
            var header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
            var payload = Base64UrlEncode("""{"exp":4102444800}""");
            return $"""
                "{header}.{payload}.signature"
                """;
        }

        private static string Base64UrlEncode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private const string FakeContextJson =
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
            """;
    }
}
#endif
