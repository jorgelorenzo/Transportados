using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.HttpOverrides;
using Transportados.Client.Services;
using Transportados.Client.Services.Storage;
using Transportados.Web.Components;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);
var serviceName = "transportados-web";
var telemetryEnvironment = builder.Environment.EnvironmentName;
var runLogFilePath = CreatePerRunLogFilePath(serviceName);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", serviceName)
        .Enrich.WithProperty("Application", "Transportados")
        .Enrich.WithProperty("EnvironmentName", telemetryEnvironment)
        .WriteTo.Console()
        .WriteTo.File(runLogFilePath, shared: true);
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024;
});
builder.Services.AddTransportadosMudServices();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorization(TransportadosClientServiceCollectionExtensions.AddTransportadosAuthorizationPolicies);
builder.Services.AddScoped<IClientStorageService, BrowserLocalStorageService>();
builder.Services.AddTransportadosClientServices("http://localhost:7306/");

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("UserName", httpContext.User.Identity?.Name ?? string.Empty);
    };
});

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledFrontendRequest");
    using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
    using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? string.Empty))
    using (LogContext.PushProperty("RequestMethod", context.Request.Method))
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled frontend exception for {Method} {Path}. TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.TraceIdentifier);
            throw;
        }
    }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Transportados.Client.Components.Routes).Assembly)
    .AllowAnonymous();

app.Run();

static string CreatePerRunLogFilePath(string serviceName)
{
    var logsRoot = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    var runFolderName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
    var runFolderPath = Path.Combine(logsRoot, runFolderName);
    Directory.CreateDirectory(runFolderPath);
    return Path.Combine(runFolderPath, $"{serviceName}.log");
}
