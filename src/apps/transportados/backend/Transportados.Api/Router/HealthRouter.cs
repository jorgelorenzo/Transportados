using Transportados.Contracts.Api.Dto;

namespace Transportados.Api.Router;

public static class HealthRouter
{
    public static IEndpointRouteBuilder MapHealthRouter(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () =>
            TypedResults.Ok(new HealthStatusDto("Transportados.Api", "ok", DateTimeOffset.UtcNow)));

        return app;
    }
}
