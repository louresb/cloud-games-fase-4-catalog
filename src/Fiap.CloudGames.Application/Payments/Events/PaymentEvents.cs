namespace Fiap.CloudGames.Application.Payments.Events;

public record PaymentLinkGeneratedEvent(
    Guid OrderId,
    string UserEmail,
    string PaymentTransactionId,
    string PaymentLinkUrl,
    string TenantId = "FIAP"
);

public record PaymentSucceededEvent(
    Guid OrderId,
    string UserEmail,
    string PaymentTransactionId,
    DateTime ProcessedAt,
    string TenantId = "FIAP"
);

public record PaymentFailedEvent(
    Guid OrderId,
    string UserEmail,
    string FailedReason,
    string TenantId = "FIAP"
);

public record PaymentRefundFailedEvent(
    Guid OrderId,
    string UserEmail,
    string FailedReason,
    string TenantId = "FIAP"
);

public record PaymentRefundedEvent(
    Guid OrderId,
    string UserEmail,
    DateTime RefundedAt,
    string TenantId = "FIAP"
);