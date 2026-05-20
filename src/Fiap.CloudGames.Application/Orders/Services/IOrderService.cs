using System.Security.Claims;
using Fiap.CloudGames.Application.Common;
using Fiap.CloudGames.Application.Orders.Dtos;

namespace Fiap.CloudGames.Application.Orders.Services;

public interface IOrderService
{
    Task<OrderResponseDto> CreateAsync(CreateOrderFromCartDto dto, string? idempotencyKey, ClaimsPrincipal user, CancellationToken ct);
    Task<OrderResponseDto> CreateAsync(CreateOrderDto dto, string? idempotencyKey, ClaimsPrincipal user, CancellationToken ct);
    Task<PagedResult<OrderResponseDto>> GetForUserAsync(string userEmail, string? status, int page, int pageSize, CancellationToken ct);
    Task<PagedResult<OrderResponseDto>> GetAllAsync(DateTime? startDate, DateTime? endDate, string? status, int page, int pageSize, CancellationToken ct);
    Task<OrderResponseDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task CancelAsync(Guid id, string reason, CancellationToken ct);
    Task SetTransactionIdAsync(Guid id, string paymentTransactionId, CancellationToken ct);
    Task MarkAsPaidAsync(Guid id, string paymentTransactionId, CancellationToken ct);
    Task<bool> RequestRefundAsync(Guid id, RefundRequestDto dto, ClaimsPrincipal user, CancellationToken ct);
    Task MarkAsRefundedAsync(Guid id, CancellationToken ct);
}