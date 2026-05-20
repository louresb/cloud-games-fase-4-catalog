using Fiap.CloudGames.Domain.Games.Search;
using Microsoft.Extensions.Hosting;

namespace Fiap.CloudGames.Infrastructure.Games.Search;

/// <summary>
/// Creates the OpenSearch index on startup if missing. Errors are swallowed
/// (the search service itself logs them) so a broken OpenSearch does not
/// take the API down.
/// </summary>
public sealed class SearchIndexInitializer(IGameSearchService search) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => search.EnsureIndexAsync(ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
