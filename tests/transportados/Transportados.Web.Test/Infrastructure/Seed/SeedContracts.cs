namespace Transportados.Web.Test.Infrastructure.Seed;

public sealed record SeededUserCredentials(string Username, string Password, string Role);

public sealed class SeedResult
{
    public required SeedProfile Profile { get; init; }
    public required string TenantName { get; init; }
    public Dictionary<string, SeededUserCredentials> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
}
