using Fiap.CloudGames.Domain.Games.Entities;
using Fiap.CloudGames.Domain.Games.Search;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;

namespace Fiap.CloudGames.Infrastructure.Games.Search;

public sealed class OpenSearchGameSearchService(
    IOpenSearchClient client,
    OpenSearchOptions options,
    ILogger<OpenSearchGameSearchService> logger) : IGameSearchService
{
    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        try
        {
            var exists = await client.Indices.ExistsAsync(options.IndexName, ct: ct);
            if (exists.Exists) return;

            var create = await client.Indices.CreateAsync(options.IndexName, c => c
                .Settings(s => s.NumberOfShards(1).NumberOfReplicas(0))
                .Map<GameSearchDocument>(m => m
                    .AutoMap()
                    .Properties(p => p
                        .Keyword(k => k.Name(n => n.Id))
                        .Keyword(k => k.Name(n => n.TenantId))
                        .Text(t => t.Name(n => n.Title).Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Text(t => t.Name(n => n.Description))
                        .Text(t => t.Name(n => n.Developer))
                        .Text(t => t.Name(n => n.Publisher))
                        .Text(t => t.Name(n => n.Genre))
                        .Number(n => n.Name(x => x.Price).Type(NumberType.ScaledFloat).ScalingFactor(100))
                        .Boolean(b => b.Name(n => n.Active))
                        .Date(d => d.Name(n => n.ReleaseDate)))),
                ct);

            if (!create.IsValid)
            {
                logger.LogWarning("OpenSearch index creation returned invalid: {Debug}", create.DebugInformation);
            }
            else
            {
                logger.LogInformation("OpenSearch index {Index} created", options.IndexName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not ensure OpenSearch index {Index} (will retry on next access)", options.IndexName);
        }
    }

    public async Task IndexAsync(Game game, CancellationToken ct)
    {
        var doc = new GameSearchDocument(
            game.Id,
            game.TenantId,
            game.Title,
            game.Description,
            game.Price,
            game.Developer,
            game.Publisher,
            game.Genre,
            game.Platforms,
            game.Active,
            game.ReleaseDate);

        var resp = await client.IndexAsync(doc, i => i
            .Index(options.IndexName)
            .Id(game.Id.ToString())
            .Refresh(OpenSearch.Net.Refresh.WaitFor), ct);

        if (!resp.IsValid)
        {
            logger.LogWarning("Failed to index game {GameId}: {Debug}", game.Id, resp.DebugInformation);
        }
    }

    public async Task RemoveAsync(Guid gameId, CancellationToken ct)
    {
        var resp = await client.DeleteAsync<GameSearchDocument>(gameId.ToString(), d => d
            .Index(options.IndexName)
            .Refresh(OpenSearch.Net.Refresh.WaitFor), ct);

        if (!resp.IsValid && resp.Result != Result.NotFound)
        {
            logger.LogWarning("Failed to delete game {GameId} from index: {Debug}", gameId, resp.DebugInformation);
        }
    }

    public async Task<IReadOnlyList<GameSearchDocument>> SearchAsync(string query, string? tenantId, int limit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<GameSearchDocument>();

        var safeLimit = Math.Clamp(limit, 1, 100);

        var response = await client.SearchAsync<GameSearchDocument>(s =>
        {
            var search = s.Index(options.IndexName).Size(safeLimit);

            return search.Query(q =>
            {
                var fuzzy = q.MultiMatch(m => m
                    .Query(query)
                    .Fields(f => f
                        .Field(d => d.Title, boost: 3.0)
                        .Field(d => d.Description, boost: 1.0)
                        .Field(d => d.Developer, boost: 1.5)
                        .Field(d => d.Publisher, boost: 1.0)
                        .Field(d => d.Genre, boost: 1.0))
                    .Fuzziness(Fuzziness.Auto)
                    .Type(TextQueryType.BestFields));

                if (string.IsNullOrWhiteSpace(tenantId)) return fuzzy;

                return q.Bool(b => b
                    .Must(fuzzy)
                    .Filter(f => f.Term(t => t.TenantId, tenantId)));
            });
        }, ct);

        if (!response.IsValid)
        {
            logger.LogWarning("OpenSearch query failed: {Debug}", response.DebugInformation);
            return Array.Empty<GameSearchDocument>();
        }

        return response.Documents.ToList();
    }
}

public sealed class OpenSearchOptions
{
    public string Uri { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "games";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool DisableSslCertValidation { get; set; } = true;
}
