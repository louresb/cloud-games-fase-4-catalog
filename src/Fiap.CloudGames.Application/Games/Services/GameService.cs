using System.Text.Json;
using Fiap.CloudGames.Application.Games.Dtos;
using Fiap.CloudGames.Domain.Games.Entities;
using Fiap.CloudGames.Domain.Games.Repositories;
using Fiap.CloudGames.Domain.Games.Search;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Application.Games.Services;

public class GameService(
    IGameRepository repository,
    IGameSearchService searchService,
    IDistributedCache cache,
    ILogger<GameService> logger) : IGameService
{
    private const string CacheKeyList = "catalog:games:list";
    private const string CacheKeyByIdPrefix = "catalog:game:";

    private static readonly DistributedCacheEntryOptions ListCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
    };

    private static readonly DistributedCacheEntryOptions ItemCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<IReadOnlyList<GameListItemDto>> GetAllAsync(CancellationToken ct)
    {
        var cached = await cache.GetStringAsync(CacheKeyList, ct);
        if (!string.IsNullOrEmpty(cached))
        {
            var hit = JsonSerializer.Deserialize<List<GameListItemDto>>(cached);
            if (hit is not null)
            {
                logger.LogDebug("catalog:games:list cache HIT ({Count})", hit.Count);
                return hit;
            }
        }

        var all = await repository.GetAllAsync(ct);
        var dtos = all.OrderBy(g => g.Title).Select(ToListItem).ToList();

        await cache.SetStringAsync(CacheKeyList, JsonSerializer.Serialize(dtos), ListCacheOptions, ct);
        logger.LogDebug("catalog:games:list cache MISS — refreshed ({Count})", dtos.Count);
        return dtos;
    }

    public async Task<GameDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var key = CacheKeyByIdPrefix + id;
        var cached = await cache.GetStringAsync(key, ct);
        if (!string.IsNullOrEmpty(cached))
        {
            var hit = JsonSerializer.Deserialize<GameDto>(cached);
            if (hit is not null)
            {
                logger.LogDebug("catalog:game cache HIT {GameId}", id);
                return hit;
            }
        }

        var game = await repository.GetByIdAsync(id, ct);
        if (game is null) return null;

        var dto = ToDto(game);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(dto), ItemCacheOptions, ct);
        return dto;
    }

    public async Task<GameDto> CreateAsync(CreateGameDto dto, CancellationToken ct)
    {
        var duplicated = await repository.ExistsByTitleAsync(dto.Title, ct);
        if (duplicated) throw new ArgumentException("Já existe um jogo com este título.");

        var entity = Game.Create(
            title: dto.Title,
            description: dto.Description,
            price: dto.Price,
            releaseDate: dto.ReleaseDate,
            developer: dto.Developer,
            publisher: dto.Publisher,
            genre: dto.Genre,
            platforms: dto.Platforms,
            tenantId: dto.TenantId);

        await repository.AddAsync(entity, ct);
        await searchService.IndexAsync(entity, ct);
        await InvalidateListCacheAsync(ct);

        return ToDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateGameDto dto, CancellationToken ct)
    {
        var current = await repository.GetByIdAsync(id, ct);
        if (current is null) return false;

        if (!string.Equals(current.Title, dto.Title, StringComparison.OrdinalIgnoreCase))
        {
            var duplicated = await repository.ExistsByTitleAsync(dto.Title, ct);
            if (duplicated) throw new ArgumentException("Já existe um jogo com este título.");
        }

        current.Update(
            title: dto.Title,
            description: dto.Description,
            price: dto.Price,
            releaseDate: dto.ReleaseDate,
            developer: dto.Developer,
            publisher: dto.Publisher,
            genre: dto.Genre,
            platforms: dto.Platforms,
            active: dto.Active);

        await repository.UpdateAsync(current, ct);
        await searchService.IndexAsync(current, ct);
        await InvalidateAsync(id, ct);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var existing = await repository.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await repository.DeleteAsync(id, ct);
        await searchService.RemoveAsync(id, ct);
        await InvalidateAsync(id, ct);

        return true;
    }

    public async Task<IReadOnlyList<GameSearchResultDto>> SearchAsync(string query, string? tenantId, int limit, CancellationToken ct)
    {
        var hits = await searchService.SearchAsync(query, tenantId, limit, ct);
        return hits.Select(d => new GameSearchResultDto(
            d.Id, d.TenantId, d.Title, d.Description, d.Price, d.Developer, d.Publisher, d.Genre, d.Active, d.ReleaseDate)).ToList();
    }

    private async Task InvalidateListCacheAsync(CancellationToken ct)
    {
        await cache.RemoveAsync(CacheKeyList, ct);
    }

    private async Task InvalidateAsync(Guid id, CancellationToken ct)
    {
        await Task.WhenAll(
            cache.RemoveAsync(CacheKeyList, ct),
            cache.RemoveAsync(CacheKeyByIdPrefix + id, ct));
    }

    private static GameListItemDto ToListItem(Game g) =>
        new(g.Id, g.Title, g.Price, g.Active);

    private static GameDto ToDto(Game g) =>
        new(g.Id, g.Title, g.Description, g.Price, g.ReleaseDate, g.Developer, g.Publisher,
            g.Genre, g.Platforms, g.Active, g.CreatedAt, g.UpdatedAt);
}
