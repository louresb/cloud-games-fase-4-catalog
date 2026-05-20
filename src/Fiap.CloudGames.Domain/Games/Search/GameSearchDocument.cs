namespace Fiap.CloudGames.Domain.Games.Search;

/// <summary>
/// Flat projection of a Game used as the OpenSearch document body.
/// Reused by the search response so callers don't pay a SQL round-trip.
/// </summary>
public sealed record GameSearchDocument(
    Guid Id,
    string TenantId,
    string Title,
    string? Description,
    decimal Price,
    string Developer,
    string Publisher,
    string? Genre,
    string? Platforms,
    bool Active,
    DateTime ReleaseDate);
