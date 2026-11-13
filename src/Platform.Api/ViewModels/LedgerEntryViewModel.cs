using Core.Domain;

namespace Platform.Api.ViewModels;

public sealed record LedgerEntryViewModel(
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

