using Core.Domain;

namespace Core.Application;

public sealed record LedgerEntryResult(
    Guid Id,
    LedgerEntryType EntryType,
    Guid ReferenceId,
    string ReferenceKind,
    decimal SignedAmount,
    string Currency,
    PaymentMethod? Method,
    string Description,
    string ActorUserId,
    DateTimeOffset OccurredAtUtc);

