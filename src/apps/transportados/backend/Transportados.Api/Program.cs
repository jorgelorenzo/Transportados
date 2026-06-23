using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using Transportados.Api.Infrastructure;
using Transportados.Api.Router;
using Transportados.Persistence.DataAccess;
using Transportados.Persistence.Seeding;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using Transportados.Platform.Core;

var builder = WebApplication.CreateBuilder(args);
var serviceName = "transportados-api";
var telemetryEnvironment = builder.Environment.EnvironmentName;

await EnsureSqlDatabaseExistsAsync(builder.Configuration);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", serviceName)
        .Enrich.WithProperty("Application", "Transportados")
        .Enrich.WithProperty("EnvironmentName", telemetryEnvironment)
        .WriteTo.Console();

    ConfigureRollingFile(loggerConfiguration, context.Configuration, serviceName);
    ConfigureAppDatabaseLogging(loggerConfiguration, context.Configuration);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "transportados-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "transportados-web";
var jwtKey = builder.Configuration["Jwt:Key"] ?? "transportados-development-signing-key-change-me-2026";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddSingleton<PasswordHashingManager>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.Configure<EmailTransportOptions>(builder.Configuration.GetSection("Email:Transport"));
builder.Services.AddScoped(sp =>
{
    var optionsBuilder = new DbContextOptionsBuilder<TransportadosDbContext>();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (builder.Configuration.GetValue<bool>("UseInMemoryDatabase") || string.IsNullOrWhiteSpace(connectionString))
    {
        optionsBuilder.UseInMemoryDatabase(builder.Configuration["DatabaseName"] ?? "transportados-api");
    }
    else
    {
        optionsBuilder.UseSqlServer(connectionString);
    }

    return optionsBuilder.Options;
});
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<DbContextOptions<TransportadosDbContext>>();
    var tenantContext = sp.GetRequiredService<ITenantContext>();
    return new TransportadosDbContext(options, tenantContext);
});
builder.Services.AddScoped<ITransportadosDataService>(sp =>
{
    var service = new EFDataServices(
        sp.GetRequiredService<TransportadosDbContext>(),
        sp.GetRequiredService<PasswordHashingManager>(),
        emailSender: sp.GetRequiredService<IEmailSender>(),
        emailTransportOptions: sp.GetRequiredService<IOptions<EmailTransportOptions>>().Value);

    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    if (httpContext?.User?.Identity?.IsAuthenticated == true)
    {
        service.LoggedUser = httpContext.LoggedUser();
    }

    return service;
});
builder.Services.Configure<SeedingOptions>(builder.Configuration.GetSection(SeedingOptions.SectionName));
builder.Services.AddScoped<ITransportadosSeedService, TransportadosSeedService>();

var app = builder.Build();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("RequestPath", httpContext.Request.Path.Value ?? string.Empty);
        diagnosticContext.Set("RequestMethod", httpContext.Request.Method);
        diagnosticContext.Set("UserId", httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
        diagnosticContext.Set("ActiveTenantId", httpContext.User.FindFirst("ActiveTenantId")?.Value ?? string.Empty);
        diagnosticContext.Set("ActiveRole", httpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty);
    };
});

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnhandledApiRequest");
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
                "Unhandled API exception for {Method} {Path}. TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.TraceIdentifier);
            throw;
        }
    }
});

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthRouter();
app.MapAuthRouter();
app.MapCustomerRouter();
app.MapSettingsRouter();
app.MapUserRouter();
app.MapPlatformRouter();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TransportadosDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    var seedingOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedingOptions>>().Value;
    if (seedingOptions.Enabled)
    {
        var seedService = scope.ServiceProvider.GetRequiredService<ITransportadosSeedService>();
        await seedService.SeedInitialDataAsync();
    }
}

app.Run();

static void ConfigureRollingFile(
    LoggerConfiguration loggerConfiguration,
    IConfiguration configuration,
    string serviceName)
{
    var section = configuration.GetSection("Serilog:RollingFile");
    if (!section.GetValue("Enabled", true))
    {
        return;
    }

    var path = string.IsNullOrWhiteSpace(section["Path"])
        ? Path.Combine("logs", $"{serviceName}-.log")
        : section["Path"]!;
    var rollingInterval = GetEnumValue(section, "RollingInterval", RollingInterval.Day);
    var retainedFileCountLimit = section.GetValue<int?>("RetainedFileCountLimit") ?? 14;
    var fileSizeLimitBytes = section.GetValue<long?>("FileSizeLimitBytes");
    var rollOnFileSizeLimit = section.GetValue("RollOnFileSizeLimit", true);
    var shared = section.GetValue("Shared", true);

    loggerConfiguration.WriteTo.File(
        path,
        rollingInterval: rollingInterval,
        retainedFileCountLimit: retainedFileCountLimit,
        fileSizeLimitBytes: fileSizeLimitBytes,
        rollOnFileSizeLimit: rollOnFileSizeLimit,
        shared: shared);
}

static void ConfigureAppDatabaseLogging(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
{
    var section = configuration.GetSection("Serilog:Database");
    if (!section.GetValue("Enabled", false) || configuration.GetValue<bool>("UseInMemoryDatabase"))
    {
        return;
    }

    var connectionStringName = string.IsNullOrWhiteSpace(section["ConnectionStringName"])
        ? "DefaultConnection"
        : section["ConnectionStringName"]!;
    var connectionString = configuration.GetConnectionString(connectionStringName);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"Serilog database logging is enabled, but ConnectionStrings:{connectionStringName} is missing.");
    }

    var sinkOptions = new MSSqlServerSinkOptions
    {
        TableName = string.IsNullOrWhiteSpace(section["TableName"]) ? "SerilogLogs" : section["TableName"]!,
        SchemaName = string.IsNullOrWhiteSpace(section["SchemaName"]) ? "dbo" : section["SchemaName"]!,
        AutoCreateSqlTable = section.GetValue("AutoCreateSqlTable", true)
    };

    var restrictedToMinimumLevel = GetEnumValue(
        section,
        "RestrictedToMinimumLevel",
        LogEventLevel.Information);

    loggerConfiguration.WriteTo.MSSqlServer(
        connectionString: connectionString,
        sinkOptions: sinkOptions,
        restrictedToMinimumLevel: restrictedToMinimumLevel);
}

static async Task EnsureSqlDatabaseExistsAsync(IConfiguration configuration)
{
    if (configuration.GetValue<bool>("UseInMemoryDatabase"))
    {
        return;
    }

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    var builder = new SqlConnectionStringBuilder(connectionString);
    var databaseName = builder.InitialCatalog;
    if (string.IsNullOrWhiteSpace(databaseName))
    {
        throw new InvalidOperationException("DefaultConnection does not include a database name.");
    }

    builder.InitialCatalog = "master";
    await using var connection = new SqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    await using var command = connection.CreateCommand();
    command.CommandText = """
IF DB_ID(@databaseName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
    EXEC sp_executesql @sql;
END
""";
    command.Parameters.AddWithValue("@databaseName", databaseName);
    await command.ExecuteNonQueryAsync();
}

static TEnum GetEnumValue<TEnum>(IConfiguration configuration, string key, TEnum defaultValue)
    where TEnum : struct
{
    return Enum.TryParse<TEnum>(configuration[key], ignoreCase: true, out var value)
        ? value
        : defaultValue;
}

public partial class Program;
