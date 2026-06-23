using System.Security.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Persistence.DataAccess;

namespace Transportados.Api.Router;

public static class TransportadosRouters
{
    public static IEndpointRouteBuilder MapCustomerRouter(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customers").RequireAuthorization().WithTags("Customers");

        group.MapGet("", async Task<IResult> (
            ITransportadosDataService dataService,
            string? search,
            string? cityFilter,
            string? stateFilter,
            string? sortBy,
            bool? sortDescending,
            int? skip,
            int? take) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.CustomerManagement);
            if (denied != null)
            {
                return denied;
            }

            return await Execute(() => dataService.GetCustomers(new CustomerQueryDto
            {
                Search = search,
                CityFilter = cityFilter,
                StateFilter = stateFilter,
                SortBy = sortBy,
                SortDescending = sortDescending.GetValueOrDefault(),
                Skip = skip.GetValueOrDefault(),
                Take = take.GetValueOrDefault(20)
            }));
        });

        group.MapGet("/list", async Task<IResult> (ITransportadosDataService dataService, string? search, int? take) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.CustomerLookup);
            return denied ?? await Execute(() => dataService.GetCustomerList(search, take.GetValueOrDefault(200)));
        });

        group.MapGet("/{id:guid}", async Task<IResult> (ITransportadosDataService dataService, Guid id) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.CustomerManagement);
            return denied ?? await ExecuteMaybe(() => dataService.GetCustomer(id));
        });

        group.MapPost("", async Task<IResult> (ITransportadosDataService dataService, CustomerDto request) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.CustomerManagement);
            return denied ?? await Execute(() => dataService.SaveCustomer(request));
        });

        group.MapDelete("/{id:guid}", async Task<IResult> (ITransportadosDataService dataService, Guid id) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.CustomerManagement);
            if (denied != null)
            {
                return denied;
            }

            return await Execute(async () =>
            {
                await dataService.DeleteCustomer(id);
                return TypedResults.NoContent();
            });
        });

        return app;
    }

    public static IEndpointRouteBuilder MapUserRouter(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization().WithTags("Users");

        group.MapGet("", async Task<IResult> (ITransportadosDataService dataService, string? search, int? skip, int? take) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.UserApiManagement);
            if (denied != null)
            {
                return denied;
            }

            return await Execute(() => dataService.GetUsers(new UserQueryDto
            {
                Search = search,
                Skip = skip.GetValueOrDefault(),
                Take = take.GetValueOrDefault(20)
            }));
        });

        group.MapGet("/list", async Task<IResult> (ITransportadosDataService dataService) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.UserApiManagement);
            return denied ?? await Execute(dataService.GetUserList);
        });

        group.MapGet("/{id:guid}", async Task<IResult> (ITransportadosDataService dataService, Guid id) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.UserApiManagement);
            return denied ?? await ExecuteMaybe(() => dataService.GetUser(id));
        });

        group.MapPost("", async Task<IResult> (ITransportadosDataService dataService, UserSaveRequestDto request) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.UserApiManagement);
            return denied ?? await Execute(() => dataService.SaveUser(request));
        });

        group.MapDelete("/{id:guid}", async Task<IResult> (ITransportadosDataService dataService, Guid id) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.UserApiManagement);
            if (denied != null)
            {
                return denied;
            }

            return await Execute(async () =>
            {
                await dataService.DeleteUser(id);
                return TypedResults.NoContent();
            });
        });

        return app;
    }

    public static IEndpointRouteBuilder MapSettingsRouter(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization().WithTags("Settings");

        group.MapGet("", async Task<IResult> (ITransportadosDataService dataService) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.SettingsRead);
            return denied ?? await Execute(dataService.GetSettings);
        });

        group.MapGet("/features", async Task<IResult> (ITransportadosDataService dataService) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.SettingsRead);
            return denied ?? await Execute(dataService.GetActiveTenantFeatureFlags);
        });

        group.MapPost("", async Task<IResult> (ITransportadosDataService dataService, SettingsDto request) =>
        {
            var denied = DenyUnless(dataService, TransportadosPermission.SettingsManagement);
            return denied ?? await Execute(() => dataService.SaveSettings(request));
        });

        return app;
    }

    public static IEndpointRouteBuilder MapPlatformRouter(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/platform").RequireAuthorization().WithTags("Platform");

        group.MapGet("/stats", async Task<IResult> (ITransportadosDataService dataService) =>
        {
            if (!IsSuperAdmin(dataService))
            {
                return Forbidden(ApiErrorCodes.TenantAccessDenied, "Superadmin permissions are required.");
            }

            return await Execute(dataService.GetPlatformStats);
        });

        group.MapGet("/tenants", async Task<IResult> (ITransportadosDataService dataService, int? page, int? pageSize) =>
        {
            if (!IsSuperAdmin(dataService))
            {
                return Forbidden(ApiErrorCodes.TenantAccessDenied, "Superadmin permissions are required.");
            }

            return await Execute(() => dataService.GetTenantList(page.GetValueOrDefault(1), pageSize.GetValueOrDefault(20)));
        });

        group.MapGet("/tenants/{id:guid}", async Task<IResult> (ITransportadosDataService dataService, Guid id) =>
        {
            if (!IsSuperAdmin(dataService))
            {
                return Forbidden(ApiErrorCodes.TenantAccessDenied, "Superadmin permissions are required.");
            }

            return await ExecuteMaybe(() => dataService.GetTenantById(id));
        });

        group.MapPost("/tenants/{id:guid}", async Task<IResult> (
            HttpContext httpContext,
            ITransportadosDataService dataService,
            Guid id,
            TenantUpdateRequestDto request,
            string? publicOrigin) =>
        {
            if (!IsSuperAdmin(dataService))
            {
                return Forbidden(ApiErrorCodes.TenantAccessDenied, "Superadmin permissions are required.");
            }

            return await ExecuteResult(async () => await dataService.UpdateTenant(id, request, publicOrigin ?? ResolvePublicOrigin(httpContext))
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
        });

        group.MapDelete("/tenants/{id:guid}", async Task<IResult> (ITransportadosDataService dataService, Guid id) =>
        {
            if (!IsSuperAdmin(dataService))
            {
                return Forbidden(ApiErrorCodes.TenantAccessDenied, "Superadmin permissions are required.");
            }

            return await ExecuteResult(async () => await dataService.DeleteTenant(id) ? TypedResults.NoContent() : TypedResults.NotFound());
        });

        group.MapPost("/tenants/{id:guid}/delete-physical", async Task<IResult> (
            ITransportadosDataService dataService,
            Guid id,
            TenantPhysicalDeleteRequestDto request) =>
        {
            if (!IsSuperAdmin(dataService))
            {
                return Forbidden(ApiErrorCodes.TenantAccessDenied, "Superadmin permissions are required.");
            }

            return await ExecuteResult(async () => await dataService.PhysicallyDeleteTenant(id, request)
                ? TypedResults.NoContent()
                : TypedResults.NotFound());
        });

        return app;
    }

    private static IResult? DenyUnless(ITransportadosDataService dataService, TransportadosPermission permission)
    {
        var user = dataService.LoggedUser;
        if (user == null)
        {
            return Unauthorized("Authenticated user context could not be resolved.");
        }

        if (permission != TransportadosPermission.Platform &&
            user.IsSuperAdmin &&
            (!user.ActiveTenantId.HasValue || user.ActiveTenantId.Value == Guid.Empty))
        {
            return Forbidden(ApiErrorCodes.ContextInvalid, "An active tenant context is required.");
        }

        return TransportadosAccessMatrix.Allows(permission, user.ActiveRole, user.IsSuperAdmin, user.ActiveTenantId)
            ? null
            : Forbidden(ApiErrorCodes.TenantAccessDenied, "The active role cannot access this Transportados resource.");
    }

    private static bool IsSuperAdmin(ITransportadosDataService dataService) =>
        dataService.LoggedUser?.IsSuperAdmin == true;

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

    private static IResult ToResult<T>(T? value) =>
        value == null ? Results.NotFound() : Results.Ok(value);

    private static async Task<IResult> ExecuteMaybe<T>(Func<Task<T?>> action)
        where T : class
    {
        try
        {
            return ToResult(await action());
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    private static async Task<IResult> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    private static async Task<IResult> Execute(Func<Task<NoContent>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    private static async Task<IResult> ExecuteResult(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            return Error(ex);
        }
    }

    private static IResult Error(Exception ex) =>
        ex switch
        {
            EntityNotFoundException => Results.NotFound(ex.Message),
            ContextAccessException contextEx => Forbidden(contextEx.Code, contextEx.Message),
            AuthenticationException authEx => Unauthorized(authEx.Message),
            UnauthorizedAccessException unauthorizedEx => Forbidden(ApiErrorCodes.TenantAccessDenied, unauthorizedEx.Message),
            InvalidOperationException => Results.BadRequest(ex.Message),
            _ => Results.Problem(ex.Message)
        };

    private static IResult Unauthorized(string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            extensions: new Dictionary<string, object?> { ["code"] = ApiErrorCodes.Unauthorized });

    private static IResult Forbidden(string code, string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
