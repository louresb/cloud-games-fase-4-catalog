using Fiap.CloudGames.Application.Orders.Services;
using Fiap.CloudGames.Application.Payments.Events;
using Fiap.CloudGames.Application.UserGamesLibrary.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fiap.CloudGames.Application.Payments.Consumers;

public class PaymentSucceededConsumer(
    ILogger<PaymentSucceededConsumer> logger, 
    ILibraryService libraryService, 
    IOrderService orderService) : IConsumer<PaymentSucceededEvent>
{
    private readonly ILogger<PaymentSucceededConsumer> _logger = logger;
    private readonly ILibraryService _libraryService = libraryService;
    private readonly IOrderService _orderService = orderService;
    
    public async Task Consume(ConsumeContext<PaymentSucceededEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("Evento recebido: Pagamento sucedido para o pedido {OrderId}.", message.OrderId);

        var order = await _orderService.GetByIdAsync(message.OrderId, CancellationToken.None);
        if (order is null)
        {
            _logger.LogWarning("Pedido {OrderId} não encontrado para processar o pagamento sucedido.", message.OrderId);
            return;
        }

        await _orderService.MarkAsPaidAsync(order.Id, message.PaymentTransactionId, CancellationToken.None);

        foreach (var item in order.Items)
        {
            await _libraryService.AddGameToLibraryAsync(order.UserId, item.GameId, CancellationToken.None);
            _logger.LogInformation("Jogo {GameId} adicionado à biblioteca do usuário {UserId}.", item.GameId, order.UserId);
        }
    }
}
