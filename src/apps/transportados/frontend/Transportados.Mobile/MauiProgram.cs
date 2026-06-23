using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Transportados.Client.Services;
using Transportados.Client.Services.Biometrics;
using Transportados.Client.Services.Storage;
using Transportados.Client.Services.Versioning;
using Transportados.Mobile.Services;

namespace Transportados.Mobile;

public static class MauiProgram
{
    private const string DefaultApiBaseUrl = "https://transportados-api.transportados.com/";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiSettings:BaseUrl"] = DefaultApiBaseUrl,
            ["ApiSettings:ShowApiBaseUrlOnLogin"] = "false"
        });
        builder.Services.AddTransportadosMudServices();
        builder.Services.AddAuthorizationCore(TransportadosClientServiceCollectionExtensions.AddTransportadosAuthorizationPolicies);
        builder.Services.AddScoped<IClientStorageService, MauiClientStorageService>();
        builder.Services.AddSingleton<IApplicationVersionProvider, MauiApplicationVersionProvider>();
        builder.Services.AddTransportadosClientServices(DefaultApiBaseUrl);
#if USE_FAKE_BIOMETRICS
        builder.Services.AddSingleton<IHttpClientFactory, FakeTransportadosApiHttpClientFactory>();
        builder.Services.AddScoped<IBiometricAuthenticationService, FakeMauiBiometricAuthenticationService>();
#else
        builder.Services.AddScoped<IBiometricAuthenticationService, MauiBiometricAuthenticationService>();
#endif

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
