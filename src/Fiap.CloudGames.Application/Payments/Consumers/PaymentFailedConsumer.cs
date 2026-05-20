using Fiap.CloudGames.Application.Orders.Services;
using Fiap.CloudGames.Application.Payments.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Application.Payments.Consumers;

public class PaymentFailedConsumer(ILogger<PaymentFailedConsumer> logger, IOrderService orderService) : IConsumer<PaymentFailedEvent>
{
    private readonly ILogger<PaymentFailedConsumer> _logger = logger;
    private readonly IOrderService _orderService = orderService;
    
    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("Evento recebido: Pagamento falhou para o pedido {OrderId}.", message.OrderId);

        var order = await _orderService.GetByIdAsync(message.OrderId, CancellationToken.None);
        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} não encontrado para processar o pagamento falhado.", message.OrderId);
            return;
        }

        await _orderService.CancelAsync(message.OrderId, message.FailedReason, CancellationToken.None);
    }
}
