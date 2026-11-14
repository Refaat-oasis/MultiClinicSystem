using System.Text.RegularExpressions;
using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Validates and normalizes Egyptian mobile/landline and international phone numbers to E.164 format.
/// </summary>
public static partial class PhoneNormalizer
{
    public static Result<string> Normalize(string? rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            return Result<string>.Failure(Error.Validation("INVALID_PHONE", "Phone number is required."));
        }

        string cleaned = FormattingCharsRegex().Replace(rawPhone.Trim(), string.Empty);
        if (cleaned.StartsWith("00", StringComparison.Ordinal))
        {
            cleaned = "+" + cleaned[2..];
        }

        if (EgyptianMobileRegex().IsMatch(cleaned) || EgyptianLandlineRegex().IsMatch(cleaned))
        {
            return Result<string>.Success($"+2{cleaned}");
        }

        if (InternationalE164Regex().IsMatch(cleaned))
        {
            return Result<string>.Success(cleaned);
        }

        return Result<string>.Failure(Error.Validation("INVALID_PHONE", "Invalid Egyptian or international phone number format."));
    }

    [GeneratedRegex("[\\s\\-\\(\\)\\.]")]
    private static partial Regex FormattingCharsRegex();

    [GeneratedRegex("^01[0125]\\d{8}$")]
    private static partial Regex EgyptianMobileRegex();

    [GeneratedRegex("^0[2-9]\\d{7,8}$")]
    private static partial Regex EgyptianLandlineRegex();

    [GeneratedRegex("^\\+[1-9]\\d{7,14}$")]
    private static partial Regex InternationalE164Regex();
}

