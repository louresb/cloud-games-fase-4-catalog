namespace Fiap.CloudGames.Application.Games.Dtos;

public sealed record GameSearchResultDto(
    Guid Id,
    string TenantId,
    string Title,
    string? Description,
    decimal Price,
    string Developer,
    string Publisher,
    string? Genre,
    bool Active,
    DateTime ReleaseDate);
