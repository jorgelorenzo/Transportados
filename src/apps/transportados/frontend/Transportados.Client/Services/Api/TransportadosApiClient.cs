using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Transportados.Contracts.Api.Dto;
using Transportados.Client.Services.Auth;

namespace Transportados.Client.Services.Api;

public sealed class TransportadosApiClient(
    IHttpClientFactory httpClientFactory,
    AuthService authService) : ITransportadosApiClient
{
    public async Task<T?> GetAsync<T>(string path)
    {
        using var response = await SendAsync(client => client.GetAsync(Normalize(path)));
        return await ReadResponse<T>(response);
    }

    public async Task<T?> PostAsync<TRequest, T>(string path, TRequest request)
    {
        using var response = await SendAsync(client => client.PostAsJsonAsync(Normalize(path), request));
        return await ReadResponse<T>(response);
    }

    public async Task<T?> PostFileAsync<T>(string path, string fileName, Stream stream, string contentType)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var bytes = memory.ToArray();

        using var response = await SendAsync(async client =>
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            content.Add(fileContent, "file", fileName);
            return await client.PostAsync(Normalize(path), content);
        });
        return await ReadResponse<T>(response);
    }

    public async Task<bool> DeleteAsync(string path)
    {
        using var response = await SendAsync(client => client.DeleteAsync(Normalize(path)));
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        await EnsureSuccess(response);
        return true;
    }

    private async Task<HttpResponseMessage> SendAsync(Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        var client = httpClientFactory.CreateClient("TransportadosApi");
        var token = await authService.GetValidAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await send(client);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        var refreshedToken = await authService.TryRefreshTokenAsync(forceRefresh: true);
        if (string.IsNullOrWhiteSpace(refreshedToken))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
        return await send(client);
    }

    private async Task<T?> ReadResponse<T>(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccess(response);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        if (response.Content is null || response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await authService.LogoutAsync();
            throw new InvalidOperationException("La sesion expiro.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync();
            var (code, detail) = ParseProblem(body);
            if (code is ApiErrorCodes.ContextInvalid or ApiErrorCodes.TenantAccessDenied)
            {
                await authService.LogoutAsync();
                throw new InvalidOperationException(detail ?? "El contexto activo ya no es valido.");
            }

            throw new InvalidOperationException(detail ?? "No tenes permisos para realizar esta accion.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
        }
    }

    private static string Normalize(string path) => path.TrimStart('/');

    private static (string? Code, string? Detail) ParseProblem(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null);
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            var root = document.RootElement;
            var code = root.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == System.Text.Json.JsonValueKind.String
                ? codeElement.GetString()
                : null;
            var detail = root.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == System.Text.Json.JsonValueKind.String
                ? detailElement.GetString()
                : null;
            return (code, detail);
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, body);
        }
    }
}
