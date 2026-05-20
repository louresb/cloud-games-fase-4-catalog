using Fiap.CloudGames.Application.Orders.Services;
using Fiap.CloudGames.Application.Payments.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Application.Payments.Consumers;

public class PaymentLinkGeneratedConsumer(
    ILogger<PaymentLinkGeneratedConsumer> logger,
    IOrderService orderService) : IConsumer<PaymentLinkGeneratedEvent>
{
    private readonly ILogger<PaymentLinkGeneratedConsumer> _logger = logger;
    private readonly IOrderService _orderService = orderService;

    public async Task Consume(ConsumeContext<PaymentLinkGeneratedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("Evento recebido: Link de pagamento gerado para o pedido {OrderId}.", message.OrderId);

        var order = await _orderService.GetByIdAsync(message.OrderId, CancellationToken.None);
        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} não encontrado para atualizar o ID da transação de pagamento.", message.OrderId);
            return;
        }

        await _orderService.SetTransactionIdAsync(order.Id, message.PaymentTransactionId, CancellationToken.None);
    }
}
