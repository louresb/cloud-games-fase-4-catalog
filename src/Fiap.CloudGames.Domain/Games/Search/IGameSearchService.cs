using Fiap.CloudGames.Domain.Games.Entities;

namespace Fiap.CloudGames.Domain.Games.Search;

public interface IGameSearchService
{
    Task EnsureIndexAsync(CancellationToken ct);
    Task IndexAsync(Game game, CancellationToken ct);
    Task RemoveAsync(Guid gameId, CancellationToken ct);

    /// <summary>
    /// Fuzzy multi-match search over Title, Description, Developer, Publisher, Genre.
    /// </summary>
    Task<IReadOnlyList<GameSearchDocument>> SearchAsync(string query, string? tenantId, int limit, CancellationToken ct);
}
