namespace Fiap.CloudGames.Application.Orders.Dtos;

/// <summary>DTO para criação de pedidos a partir de um carrinho.</summary>
/// <param name="CartId">Identificador do carrinho.</param>
public record CreateOrderFromCartDto(
    Guid CartId
);
