using System.Security.Claims;
using Fiap.CloudGames.Application.Common;
using Fiap.CloudGames.Application.Orders.Dtos;
using Fiap.CloudGames.Application.Payments.Commands;
using Fiap.CloudGames.Domain.Carts.Repositories;
using Fiap.CloudGames.Domain.Games.Repositories;
using Fiap.CloudGames.Domain.Orders.Entities;
using Fiap.CloudGames.Domain.Orders.Enums;
using Fiap.CloudGames.Domain.Orders.Repositories;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace Fiap.CloudGames.Application.Orders.Services;

public class OrderService(
    ISendEndpointProvider sendEndpointProvider, 
    IConfiguration configuration,
    IOrderRepository repository, 
    IGameRepository gameRepository, 
    ICartRepository cartRepository) : IOrderService
{
    private readonly ISendEndpointProvider _sendEndpointProvider = sendEndpointProvider;
    private readonly IConfiguration _configuration = configuration;
    private readonly IOrderRepository _repository = repository;
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ICartRepository _cartRepository = cartRepository;

    private async Task<ISendEndpoint> GetPaymentsCommandsQueueEndpoint()
    {
        var queuePayments = _configuration["Queues:Payments:Commands"] ?? throw new InvalidOperationException("Configuração de fila de comandos de pagamentos não encontrada.");
        var sendEndpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{queuePayments}"));
        return sendEndpoint;
    }

    public async Task<OrderResponseDto> CreateAsync(CreateOrderFromCartDto dto, string? idempotencyKey, ClaimsPrincipal user, CancellationToken ct)
    {
        var cart = await _cartRepository.GetByIdAsync(dto.CartId, ct);
        if (cart is null) 
            throw new ArgumentException("Carrinho não encontrado.", nameof(dto.CartId));
        
        var createOrderDto = new CreateOrderDto(
            UserId: cart.UserId,
            Items: cart.Items
                .Select(i => new CreateOrderItemDto(i.GameId, 1, i.UnitPrice))
                .ToList()
        );

        var result = await CreateAsync(createOrderDto, idempotencyKey, user, ct);

        cart.Clear();
        await _cartRepository.UpdateAsync(cart, ct);

        return result;
    }

    public async Task<OrderResponseDto> CreateAsync(CreateOrderDto dto, string? idempotencyKey, ClaimsPrincipal user, CancellationToken ct)
    {
        var tokenUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(tokenUserId, out var authUserId) || authUserId != dto.UserId)
            throw new UnauthorizedAccessException("Usuário não autorizado a criar pedido para outro usuário.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _repository.GetByIdempotencyKeyAsync(dto.UserId, idempotencyKey!, ct);
            if (existing is not null) return ToDto(existing);
        }

        var gameIds = dto.Items.Select(i => i.GameId).Distinct().ToList();
        var gamesById = new Dictionary<Guid, string>(gameIds.Count);
        foreach (var gid in gameIds)
        {
            var g = await _gameRepository.GetByIdAsync(gid, ct);
            if (g is null) throw new ArgumentException($"Jogo não encontrado: {gid}", nameof(dto.Items));
            gamesById[gid] = g.Title;
        }

        var orderItems = dto.Items
            .Select(i => OrderItem.Create(
                gameId: i.GameId,
                title: gamesById[i.GameId],
                quantity: i.Quantity,
                unitPrice: i.UnitPrice))
            .ToList();

        var customerEmail = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        var order = Order.Create(
            userId: dto.UserId,
            customerEmail: customerEmail,
            items: orderItems,
            idempotencyKey: idempotencyKey
        );

        await _repository.AddAsync(order, ct);

        var sendEndpoint = await GetPaymentsCommandsQueueEndpoint();
        await sendEndpoint.Send(new InitiatePaymentCommand
        (
            OrderId: order.Id,
            Amount: order.TotalValue,
            UserId: order.UserId,
            UserEmail: customerEmail
        ), ct);
        
        return ToDto(order);
    }

    public async Task<PagedResult<OrderResponseDto>> GetForUserAsync(string userEmail, string? status, int page, int pageSize, CancellationToken ct)
    {
        var (orders, total) = await _repository.QueryForUserAsync(userEmail, status, page, pageSize, ct);
        var dtos = orders.Select(ToDto).ToList();
        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return new PagedResult<OrderResponseDto>(dtos, page, pageSize, total, totalPages);
    }

    public async Task<PagedResult<OrderResponseDto>> GetAllAsync(DateTime? startDate, DateTime? endDate, string? status, int page, int pageSize, CancellationToken ct)
    {
        var (orders, total) = await _repository.QueryAllAsync(startDate, endDate, status, page, pageSize, ct);
        var dtos = orders.Select(ToDto).ToList();
        var totalPages = (int)Math.Ceiling((double)total / pageSize);
        return new PagedResult<OrderResponseDto>(dtos, page, pageSize, total, totalPages);
    }

    public async Task<OrderResponseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        return order is null ? null : ToDto(order);
    }

    public async Task CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        if (order is null) throw new ArgumentException("Pedido não encontrado.", nameof(id));

        order.Cancel(reason);
        await _repository.UpdateAsync(order, ct);
    }

    public async Task SetTransactionIdAsync(Guid id, string paymentTransactionId, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        if (order is null) throw new ArgumentException("Pedido não encontrado.", nameof(id));

        order.SetTransactionId(paymentTransactionId);
        await _repository.UpdateAsync(order, ct);
    }

    public async Task MarkAsPaidAsync(Guid id, string paymentTransactionId, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        if (order is null) throw new ArgumentException("Pedido não encontrado.", nameof(id));

        order.MarkPaid(paymentTransactionId);
        await _repository.UpdateAsync(order, ct);
    }

    public async Task<bool> RequestRefundAsync(Guid id, RefundRequestDto dto, ClaimsPrincipal user, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        if (order is null) return false;

        var isAdmin = user.IsInRole("Administrator");
        var email = user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var isOwner = string.Equals(order.CustomerEmail, email, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isOwner) throw new UnauthorizedAccessException("Sem permissão para solicitar estorno deste pedido.");

        if (order.Status is OrderStatus.Refunded or OrderStatus.Cancelled)
            throw new InvalidOperationException("Pedido não elegível para estorno.");
        if ((DateTime.UtcNow - order.CreatedAt).TotalDays > 30)
            throw new InvalidOperationException("Prazo para estorno expirou.");

        order.RequestRefund(dto.Reason, DateTime.UtcNow);

        var sendEndpoint = await GetPaymentsCommandsQueueEndpoint();
        await sendEndpoint.Send(new RefundPaymentCommand
        (
            OrderId: order.Id,
            UserId: order.UserId,
            Reason: dto.Reason
        ), ct);

        await _repository.UpdateAsync(order, ct);
        return true;
    }

    public async Task MarkAsRefundedAsync(Guid id, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        if (order is null) throw new ArgumentException("Pedido não encontrado.", nameof(id));

        order.MarkRefunded(DateTime.UtcNow);
        await _repository.UpdateAsync(order, ct);
    }

    private static OrderResponseDto ToDto(Order o) =>
        new(
            o.Id,
            o.UserId,
            o.CustomerEmail,
            o.CreatedAt,
            o.TotalValue,
            o.Status,
            o.RefundRequested,
            o.PaymentTransactionId,
            o.Items.Select(i =>
                new OrderItemResponseDto(i.GameId, i.Title, i.Quantity, i.UnitPrice, i.LineTotal)
            ).ToList()
        );
}