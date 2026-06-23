namespace Transportados.Client.Services.Api;

public interface ITransportadosApiClient
{
    Task<T?> GetAsync<T>(string path);
    Task<T?> PostAsync<TRequest, T>(string path, TRequest request);
    Task<T?> PostFileAsync<T>(string path, string fileName, Stream stream, string contentType);
    Task<bool> DeleteAsync(string path);
}
