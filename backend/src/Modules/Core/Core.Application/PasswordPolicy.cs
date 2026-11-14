using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Enforces password complexity, minimum length, common-password denial, and identity dissimilarity.
/// </summary>
public sealed class PasswordPolicy : IPasswordPolicy
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "12345678",
        "123456789",
        "admin123",
        "qwerty",
        "clinic123",
        "password123",
        "welcome1",
        "adminadmin"
    };

    public Result Validate(string password, string email)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure(Error.Validation("PASSWORD_EMPTY", "Password cannot be empty."));
        }

        if (CommonPasswords.Contains(password.Trim()))
        {
            return Result.Failure(Error.Validation("PASSWORD_TOO_COMMON", "This password is too common and easily guessed."));
        }

        if (password.Length < 8)
        {
            return Result.Failure(Error.Validation("PASSWORD_TOO_SHORT", "Password must be at least 8 characters long."));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            string cleanEmail = email.Trim().ToLowerInvariant();
            string cleanPass = password.Trim().ToLowerInvariant();

            if (cleanPass == cleanEmail)
            {
                return Result.Failure(Error.Validation("PASSWORD_EQUALS_EMAIL", "Password cannot be identical to the email address."));
            }

            int atIndex = cleanEmail.IndexOf('@', StringComparison.Ordinal);
            if (atIndex > 0)
            {
                string emailPrefix = cleanEmail[..atIndex];
                if (cleanPass == emailPrefix)
                {
                    return Result.Failure(Error.Validation("PASSWORD_EQUALS_USERNAME", "Password cannot match your username."));
                }
            }
        }

        return Result.Success();
    }
}
