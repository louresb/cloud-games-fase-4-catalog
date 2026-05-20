using Fiap.CloudGames.Domain.Orders.Enums;

namespace Fiap.CloudGames.Domain.Orders.Entities;

/// <summary>Aggregate root of Order.</summary>
public class Order
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;

    private readonly List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items;

    public decimal TotalValue { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;

    public bool RefundRequested { get; private set; }
    public string? RefundReason { get; private set; }
    public DateTime? RefundRequestDate { get; private set; }
    public DateTime? RefundDate { get; private set; }

    public string? PaymentTransactionId { get; private set; }

    public string? IdempotencyKey { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    private Order() { }

    private Order(
        Guid id,
        Guid userId,
        string customerEmail,
        IEnumerable<OrderItem> items,
        string? paymentTransactionId,
        string? idempotencyKey)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId obrigatório.", nameof(userId));

        Id = id;
        UserId = userId;
        CustomerEmail = (customerEmail ?? string.Empty).Trim();

        _items.AddRange(items ?? throw new ArgumentNullException(nameof(items)));
        if (_items.Count == 0) throw new ArgumentException("Pedido deve conter ao menos um item.", nameof(items));

        TotalValue = _items.Sum(i => i.LineTotal);

        PaymentTransactionId = paymentTransactionId;

        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = null;
        Status = OrderStatus.PendingPayment;
        RefundRequested = false;
    }

    public static Order Create(
        Guid userId,
        string? customerEmail,
        IEnumerable<OrderItem> items,
        string? paymentTransactionId = null,
        string? idempotencyKey = null)
        => new(
            id: Guid.NewGuid(),
            userId: userId,
            customerEmail: customerEmail ?? string.Empty,
            items: items,
            paymentTransactionId: paymentTransactionId,
            idempotencyKey: idempotencyKey
        );
    
    public void SetTransactionId(string paymentTransactionId)
    {
        if (string.IsNullOrWhiteSpace(paymentTransactionId))
            throw new ArgumentException("PaymentTransactionId obrigatório.", nameof(paymentTransactionId));
        PaymentTransactionId = paymentTransactionId.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaid(string paymentTransactionId)
    {
        if (string.IsNullOrWhiteSpace(paymentTransactionId))
            throw new ArgumentException("PaymentTransactionId obrigatório.", nameof(paymentTransactionId));
        if (Status == OrderStatus.Paid) return;
        if (Status is OrderStatus.Cancelled or OrderStatus.Refunded)
            throw new InvalidOperationException("Não é possível pagar um pedido cancelado/estornado.");

        SetTransactionId(paymentTransactionId);
        Status = OrderStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequestRefund(string reason, DateTime utcNow)
    {
        if (RefundRequested) throw new InvalidOperationException("Pedido já possui solicitação de estorno.");
        if (Status is OrderStatus.Cancelled or OrderStatus.Refunded)
            throw new InvalidOperationException("Pedido não elegível para estorno.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Motivo do estorno é obrigatório.", nameof(reason));

        RefundRequested = true;
        RefundReason = reason.Trim();
        RefundRequestDate = utcNow;
        Status = OrderStatus.RefundRequested;
        UpdatedAt = utcNow;
    }

    public void MarkRefunded(DateTime utcNow)
    {
        if (!RefundRequested) throw new InvalidOperationException("Estorno não solicitado.");
        Status = OrderStatus.Refunded;
        RefundDate = utcNow;
        UpdatedAt = utcNow;
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Paid or OrderStatus.Refunded)
            throw new InvalidOperationException("Não é possível cancelar um pedido pago/estornado.");
        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        RefundReason = string.IsNullOrWhiteSpace(reason) ? RefundReason : reason.Trim();
    }
}