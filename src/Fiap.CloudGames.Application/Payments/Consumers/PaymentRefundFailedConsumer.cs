using Fiap.CloudGames.Application.Orders.Services;
using Fiap.CloudGames.Application.Payments.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Application.Payments.Consumers;

public class PaymentRefundFailedConsumer
(
    ILogger<PaymentRefundFailedConsumer> logger,
    IOrderService orderService
) : IConsumer<PaymentRefundFailedEvent>
{
    private readonly ILogger<PaymentRefundFailedConsumer> _logger = logger;
    private readonly IOrderService _orderService = orderService;

    public async Task Consume(ConsumeContext<PaymentRefundFailedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("Evento recebido: Falha no reembolso de pagamento para o pedido {OrderId}.", message.OrderId);

        var order = await _orderService.GetByIdAsync(message.OrderId, CancellationToken.None);
        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} não encontrado para processar a falha no reembolso de pagamento.", message.OrderId);
            return;
        }

        await _orderService.CancelAsync(message.OrderId, message.FailedReason, CancellationToken.None);
    }
}
