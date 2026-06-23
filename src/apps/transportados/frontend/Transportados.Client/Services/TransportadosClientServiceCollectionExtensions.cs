using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using Transportados.Client.Navigation;
using Transportados.Client.Services.Api;
using Transportados.Client.Services.Auth;
using Transportados.Client.Services.Biometrics;
using Transportados.Client.Services.Versioning;

namespace Transportados.Client.Services;

public static class TransportadosClientServiceCollectionExtensions
{
    public static IServiceCollection AddTransportadosMudServices(this IServiceCollection services)
    {
        services.AddMudServices(configuration =>
        {
            configuration.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            configuration.SnackbarConfiguration.PreventDuplicates = true;
            configuration.SnackbarConfiguration.NewestOnTop = true;
            configuration.SnackbarConfiguration.ShowCloseIcon = true;
            configuration.SnackbarConfiguration.VisibleStateDuration = 5000;
            configuration.SnackbarConfiguration.HideTransitionDuration = 200;
            configuration.SnackbarConfiguration.ShowTransitionDuration = 200;

            configuration.ResizeOptions = new ResizeOptions
            {
                NotifyOnBreakpointOnly = true,
                SuppressInitEvent = false
            };
        });

        return services;
    }

    public static IServiceCollection AddTransportadosClientServices(this IServiceCollection services, string defaultApiBaseUrl)
    {
        services.AddCascadingAuthenticationState();
        services.AddScoped<ITransportadosApiClient, TransportadosApiClient>();
        services.TryAddScoped<IBiometricAuthenticationService, NoOpBiometricAuthenticationService>();
        services.TryAddSingleton<BiometricAuthenticationSessionState>();
        services.TryAddSingleton<IApplicationVersionProvider, AssemblyApplicationVersionProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
        services.AddScoped<CustomAuthStateProvider>();
        services.AddScoped<IAuthStateNotifier>(sp => (IAuthStateNotifier)sp.GetRequiredService<AuthenticationStateProvider>());
        services.AddScoped<AuthService>();
        services.AddScoped<TransportadosNavigationService>();

        services.AddHttpClient("TransportadosApi", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["ApiSettings:BaseUrl"] ?? defaultApiBaseUrl;
            if (!baseUrl.EndsWith('/'))
            {
                baseUrl += "/";
            }

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        });

        return services;
    }

    public static void AddTransportadosAuthorizationPolicies(AuthorizationOptions options)
    {
        AddTenantFeaturePolicy(options, TransportadosTenantFeatureAccess.AppearancePolicy, TransportadosTenantFeatureAccess.Appearance);
        AddTenantFeaturePolicy(options, TransportadosTenantFeatureAccess.EmailPolicy, TransportadosTenantFeatureAccess.Email);
    }

    private static void AddTenantFeaturePolicy(AuthorizationOptions options, string policyName, string feature)
    {
        options.AddPolicy(policyName, policy => policy.RequireAssertion(context =>
            TransportadosTenantFeatureAccess.IsEnabled(context.User, feature)));
    }
}
