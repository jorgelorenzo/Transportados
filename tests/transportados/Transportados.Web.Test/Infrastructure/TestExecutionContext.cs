using Transportados.Web.Test.Infrastructure.Hosting;
using Transportados.Web.Test.Infrastructure.Seed;

namespace Transportados.Web.Test.Infrastructure;

public sealed class TestExecutionContext : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _resources = new();

    public required string TestId { get; init; }
    public required string ArtifactsDirectory { get; init; }
    public required string ApiBaseUrl { get; init; }
    public required string WebBaseUrl { get; init; }
    public required SeedResult SeedResult { get; init; }

    public IReadOnlyList<HostRunHandle> HostHandles =>
        _resources.OfType<HostRunHandle>().ToList();

    public void RegisterResource(IAsyncDisposable resource) => _resources.Add(resource);

    internal void TakeOwnershipFrom(TestExecutionContext other)
    {
        _resources.AddRange(other._resources);
        other._resources.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = _resources.Count - 1; i >= 0; i--)
        {
            await _resources[i].DisposeAsync();
        }
    }
}
