using Fiap.CloudGames.Domain.Games.Entities;
using Fiap.CloudGames.Domain.Games.Search;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Infrastructure.Games.Search;

/// <summary>
/// No-op implementation used when OpenSearch is not configured. Keeps API surface
/// intact so the rest of the system runs unchanged in local/dev when the search
/// engine is unavailable.
/// </summary>
public sealed class NullGameSearchService(ILogger<NullGameSearchService> logger) : IGameSearchService
{
    public Task EnsureIndexAsync(CancellationToken ct)
    {
        logger.LogInformation("OpenSearch disabled. Search index operations will be no-ops.");
        return Task.CompletedTask;
    }

    public Task IndexAsync(Game game, CancellationToken ct) => Task.CompletedTask;
    public Task RemoveAsync(Guid gameId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<GameSearchDocument>> SearchAsync(string query, string? tenantId, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GameSearchDocument>>(Array.Empty<GameSearchDocument>());
}
