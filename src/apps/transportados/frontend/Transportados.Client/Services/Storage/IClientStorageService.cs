namespace Transportados.Client.Services.Storage;

public interface IClientStorageService
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
    Task RemoveAsync(string key);
}