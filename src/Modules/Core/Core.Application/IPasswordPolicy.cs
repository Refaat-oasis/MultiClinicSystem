using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Enforces business password strength and security rules.
/// </summary>
public interface IPasswordPolicy
{
    Result Validate(string password, string email);
}
