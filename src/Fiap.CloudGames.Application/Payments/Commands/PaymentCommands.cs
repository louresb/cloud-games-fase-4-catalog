namespace Fiap.CloudGames.Application.Payments.Commands;

/// <summary>
/// <para>Comando para iniciar o processo de pagamento de um pedido.</para>
/// <para>[Order] -> [Payments]</para>
/// </summary>
/// <param name="OrderId"></param>
/// <param name="Amount"></param>
/// <param name="UserId"></param>
/// <param name="UserEmail"></param>
public record InitiatePaymentCommand(
    Guid OrderId,
    decimal Amount,
    Guid UserId,
    string UserEmail
);

/// <summary>
/// <para>Comando para reembolsar um pagamento.</para>
/// <para>[Order] -> [Payments]</para>
/// </summary>
/// <param name="OrderId"></param>
/// <param name="UserId"></param>
/// <param name="Reason"></param>
public record RefundPaymentCommand(
    Guid OrderId,
    Guid UserId,
    string Reason
);