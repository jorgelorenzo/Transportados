using Microsoft.JSInterop;

namespace Transportados.Client.Services.Storage;

public sealed class BrowserLocalStorageService(IJSRuntime jsRuntime) : IClientStorageService
{
    public async Task SetAsync(string key, string value)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    public async Task<string?> GetAsync(string key)
    {
        return await jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
    }

    public async Task RemoveAsync(string key)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }
}