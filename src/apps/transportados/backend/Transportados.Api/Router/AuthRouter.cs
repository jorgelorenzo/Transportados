using Microsoft.AspNetCore.Authorization;
using Transportados.Api.Infrastructure;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Persistence.DataAccess;
using Transportados.Platform.AspNetCore;
using Transportados.Platform.Core;

namespace Transportados.Api.Router;

public static class AuthRouter
{
    private const string SwaggerGroup = "Auth";
    private const string DefaultJwtIssuer = "transportados-api";
    private const string DefaultJwtAudience = "transportados-web";
    private const string DefaultJwtKey = "transportados-development-signing-key-change-me-2026";

    public static IEndpointRouteBuilder MapAuthRouter(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/gettoken",
            [AllowAnonymous]
            async (IConfiguration configuration, ITransportadosDataService dataService, ILoggerFactory loggerFactory, LoginRequestDto request) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest("Username and password are required.");
                }

                try
                {
                    var context = await dataService.Login(request);
                    if (context == null)
                    {
                        return Results.Unauthorized();
                    }

                    return Results.Json(BuildToken(configuration, context));
                }
                catch (ContextSelectionRequiredException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Problem(ex, loggerFactory, "Auth gettoken failed.");
                }
            })
            .WithTags(SwaggerGroup)
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/auth/getvercode",
            [AllowAnonymous]
            async (HttpContext httpContext, ITransportadosDataService dataService, ILoggerFactory loggerFactory, PasswordRecoveryCodeRequestDto request) =>
                await RequestVerificationCode(
                    httpContext,
                    dataService,
                    loggerFactory,
                    request.Email,
                    request.PublicOrigin))
            .WithTags(SwaggerGroup)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        app.MapPost("/auth/changepasswordwithcode",
            [AllowAnonymous]
            async (ITransportadosDataService dataService, ILoggerFactory loggerFactory, ChangePasswordWithCodeRequestDto request) =>
                await ChangePasswordWithCode(
                    dataService,
                    loggerFactory,
                    request.Email,
                    request.VerificationCode,
                    request.NewPassword))
            .WithTags(SwaggerGroup)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        app.MapPost("/user/getvercode",
            [AllowAnonymous]
            async (HttpContext httpContext, ITransportadosDataService dataService, ILoggerFactory loggerFactory, string username, string? publicOrigin) =>
                await RequestVerificationCode(httpContext, dataService, loggerFactory, username, publicOrigin))
            .WithTags(SwaggerGroup)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        app.MapPost("/user/changepasswordwithcode",
            [AllowAnonymous]
            async (ITransportadosDataService dataService, ILoggerFactory loggerFactory, string email, string verificationCode, string newPassword) =>
                await ChangePasswordWithCode(dataService, loggerFactory, email, verificationCode, newPassword))
            .WithTags(SwaggerGroup)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

        app.MapPost("/auth/select-context",
            [Authorize]
            async (HttpContext httpContext, IConfiguration configuration, ITransportadosDataService dataService, ILoggerFactory loggerFactory, SelectContextRequestDto request) =>
            {
                try
                {
                    dataService.LoggedUser = httpContext.LoggedUser();
                    var userId = dataService.LoggedUser?.UserId ?? Guid.Empty;
                    if (userId == Guid.Empty)
                    {
                        return Results.Unauthorized();
                    }

                    var context = await dataService.BuildUserContext(userId, request.ActiveRole, request.ActiveTenantId, request.AppContext);
                    return context == null
                        ? Results.Unauthorized()
                        : Results.Json(BuildToken(configuration, context));
                }
                catch (ContextSelectionRequiredException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Problem(ex, loggerFactory, "Auth select-context failed.");
                }
            })
            .WithTags(SwaggerGroup)
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/auth/refresh-token",
            [Authorize]
            async (HttpContext httpContext, IConfiguration configuration, ITransportadosDataService dataService, ILoggerFactory loggerFactory) =>
            {
                try
                {
                    dataService.LoggedUser = httpContext.LoggedUser();
                    var userId = dataService.LoggedUser?.UserId ?? Guid.Empty;
                    if (userId == Guid.Empty)
                    {
                        return Results.Unauthorized();
                    }

                    var context = await dataService.BuildUserContext(
                        userId,
                        dataService.LoggedUser?.ActiveRole,
                        dataService.LoggedUser?.ActiveTenantId,
                        dataService.LoggedUser?.AppContext);

                    return context == null
                        ? Results.Unauthorized()
                        : Results.Json(BuildToken(configuration, context));
                }
                catch (Exception ex)
                {
                    return Problem(ex, loggerFactory, "Auth refresh-token failed.");
                }
            })
            .WithTags(SwaggerGroup)
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/auth/me",
            [Authorize]
            async (HttpContext httpContext, ITransportadosDataService dataService) =>
            {
                var context = httpContext.LoggedUser();
                if (context == null)
                {
                    return Results.Unauthorized();
                }

                dataService.LoggedUser = context;
                var refreshedContext = await dataService.BuildUserContext(
                    context.UserId,
                    context.ActiveRole,
                    context.ActiveTenantId,
                    context.AppContext);
                return refreshedContext == null ? Results.Unauthorized() : Results.Ok(refreshedContext);
            })
            .WithTags(SwaggerGroup)
            .Produces<AuthenticatedContextDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/auth/demo-users",
            [AllowAnonymous]
            async (ITransportadosDataService dataService) => Results.Ok(await dataService.GetDemoUsers()))
            .WithTags(SwaggerGroup)
            .Produces<List<DemoUserDto>>(StatusCodes.Status200OK);

        app.MapPost("/auth/demo-login",
            [AllowAnonymous]
            async (IConfiguration configuration, ITransportadosDataService dataService, ILoggerFactory loggerFactory, DemoLoginRequestDto request) =>
            {
                try
                {
                    var context = await dataService.DemoLogin(request.UserId);
                    return Results.Json(BuildToken(configuration, context));
                }
                catch (Exception ex)
                {
                    return Problem(ex, loggerFactory, "Auth demo-login failed.");
                }
            })
            .WithTags(SwaggerGroup)
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/auth/register",
            [AllowAnonymous]
            async (HttpContext httpContext, ITransportadosDataService dataService, ILoggerFactory loggerFactory, RegisterCompanyAccountRequestDto request, string? publicOrigin) =>
            {
                try
                {
                    var response = await dataService.RegisterCompanyAccount(request, publicOrigin ?? ResolvePublicOrigin(httpContext));
                    return Results.Accepted($"/api/platform/tenants/{response.Tenant.Id:D}", response);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (Exception ex)
                {
                    return Problem(ex, loggerFactory, "Auth register failed.");
                }
            })
            .WithTags(SwaggerGroup)
            .Produces<RegisterCompanyAccountResponseDto>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> RequestVerificationCode(
        HttpContext httpContext,
        ITransportadosDataService dataService,
        ILoggerFactory loggerFactory,
        string email,
        string? publicOrigin)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest("Debe indicar un email.");
        }

        try
        {
            await dataService.GenerateVerificationCode(email, publicOrigin ?? ResolvePublicOrigin(httpContext));
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return Problem(ex, loggerFactory, "Auth verification-code request failed.");
        }
    }

    private static async Task<IResult> ChangePasswordWithCode(
        ITransportadosDataService dataService,
        ILoggerFactory loggerFactory,
        string email,
        string verificationCode,
        string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest("Debe indicar un email.");
        }

        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            return Results.BadRequest("Debe indicar el codigo de verificacion.");
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Results.BadRequest("Debe indicar la nueva clave.");
        }

        try
        {
            await dataService.ChangePasswordWithCode(email, verificationCode, newPassword);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return Problem(ex, loggerFactory, "Auth change-password-with-code failed.");
        }
    }

    private static string? ResolvePublicOrigin(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var scheme = request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto)
            ? forwardedProto.FirstOrDefault()
            : request.Scheme;
        var host = request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost)
            ? forwardedHost.FirstOrDefault()
            : request.Host.Value;

        return string.IsNullOrWhiteSpace(scheme) || string.IsNullOrWhiteSpace(host)
            ? null
            : $"{scheme}://{host}";
    }

    private static string BuildToken(IConfiguration configuration, AuthenticatedContextDto context)
    {
        var key = configuration["Jwt:Key"] ?? DefaultJwtKey;
        if (key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters long.");
        }

        return PlatformJwtTokenBuilder.Build(
            ToPlatformClaimSet(context),
            configuration["Jwt:Issuer"] ?? DefaultJwtIssuer,
            configuration["Jwt:Audience"] ?? DefaultJwtAudience,
            key,
            Roles.SuperAdmin);
    }

    private static PlatformUserClaimSet ToPlatformClaimSet(AuthenticatedContextDto context) =>
        new()
        {
            Id = context.UserId,
            Email = context.Email,
            FullName = context.FullName,
            IsSuperAdmin = context.IsSuperAdmin,
            IsDemo = context.IsDemo,
            TenantMemberships = context.TenantMemberships.Select(m => new PlatformTenantMemberClaim
            {
                TenantMemberId = m.TenantMemberId,
                TenantId = m.TenantId,
                TenantName = m.TenantName,
                Role = m.Role
            }).ToList(),
            AllowedTenantIds = context.AllowedTenantIds,
            ActiveRole = context.ActiveRole,
            ActiveTenantId = context.ActiveTenantId,
            AppContext = context.AppContext,
            DefaultRole = context.DefaultRole
        };

    private static IResult Problem(Exception ex, ILoggerFactory loggerFactory, string message)
    {
        loggerFactory.CreateLogger(nameof(AuthRouter)).LogError(ex, "{Message}", message);

        return Results.Problem(
            title: ex.GetType().Name,
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
