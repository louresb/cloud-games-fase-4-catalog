using Fiap.CloudGames.Application.Orders.Services;
using Fiap.CloudGames.Application.Payments.Events;
using Fiap.CloudGames.Application.UserGamesLibrary.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Application.Payments.Consumers;

public class PaymentRefundedConsumer(
    ILogger<PaymentRefundedConsumer> logger, 
    ILibraryService libraryService, 
    IOrderService orderService) : IConsumer<PaymentRefundedEvent>
{
    private readonly ILogger<PaymentRefundedConsumer> _logger = logger;
    private readonly ILibraryService _libraryService = libraryService;
    private readonly IOrderService _orderService = orderService;
    
    public async Task Consume(ConsumeContext<PaymentRefundedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("Evento recebido: Pagamento reembolsado para o pedido {OrderId}.", message.OrderId);

        var order = await _orderService.GetByIdAsync(message.OrderId, CancellationToken.None);
        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} não encontrado para processar o reembolso de pagamento.", message.OrderId);
            return;
        }

        await _orderService.MarkAsRefundedAsync(order.Id, CancellationToken.None);

        foreach (var item in order.Items)
        {
            await _libraryService.RemoveGameFromLibraryAsync(order.UserId, item.GameId, CancellationToken.None);
            _logger.LogInformation("Jogo {GameId} removido da biblioteca do usuário {UserId}.", item.GameId, order.UserId);
        }
    }
}
