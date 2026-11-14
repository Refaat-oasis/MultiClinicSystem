using BuildingBlocks.Domain;

namespace Core.Contracts;

/// <summary>
/// Cross-module contract for creating passwordless invited user accounts and dispatching activation codes.
/// </summary>
public interface IAccountInvitations
{
    Task<Result<Guid>> InviteAsync(
        string realm,
        string email,
        string fullName,
        string role,
        Guid? clinicId,
        CancellationToken cancellationToken = default);
}
