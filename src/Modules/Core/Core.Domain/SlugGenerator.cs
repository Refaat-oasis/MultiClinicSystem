using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Validates and suggests public clinic URL slugs following Arabic transliteration and reserved-word rules.
/// </summary>
public static partial class SlugGenerator
{
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "app", "c", "login", "portal", "platform", "static",
        "assets", "health", "www", "auth", "clinics", "profile", "settings",
        "onboarding", "system", "root", "support", "help"
    };

    private static readonly Dictionary<char, string> ArabicTransliterationMap = new()
    {
        ['ا'] = "a", ['أ'] = "a", ['إ'] = "i", ['آ'] = "a", ['ى'] = "a",
        ['ب'] = "b", ['ت'] = "t", ['ث'] = "th", ['ج'] = "g", ['ح'] = "h",
        ['خ'] = "kh", ['د'] = "d", ['ذ'] = "z", ['ر'] = "r", ['ز'] = "z",
        ['س'] = "s", ['ش'] = "sh", ['ص'] = "s", ['ض'] = "d", ['ط'] = "t",
        ['ظ'] = "z", ['ع'] = "a", ['غ'] = "gh", ['ف'] = "f", ['ق'] = "q",
        ['ك'] = "k", ['ل'] = "l", ['م'] = "m", ['ن'] = "n", ['ه'] = "h",
        ['ة'] = "a", ['و'] = "w", ['ؤ'] = "o", ['ي'] = "y", ['ئ'] = "e",
        ['ء'] = "a"
    };

    public static Result<string> Validate(string? rawSlug)
    {
        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            return Result<string>.Failure(Error.Validation("SLUG_INVALID_LENGTH", "Slug must be between 3 and 40 characters."));
        }

        string normalized = rawSlug.Trim().ToLowerInvariant();
        if (normalized.Length < 3 || normalized.Length > 40)
        {
            return Result<string>.Failure(Error.Validation("SLUG_INVALID_LENGTH", "Slug must be between 3 and 40 characters."));
        }

        if (!ValidSlugRegex().IsMatch(normalized) || normalized.Contains("--", StringComparison.Ordinal))
        {
            return Result<string>.Failure(Error.Validation(
                "SLUG_INVALID_FORMAT",
                "Slug may contain lowercase letters, digits, and single hyphens only, with no leading or trailing hyphens."));
        }

        if (ReservedWords.Contains(normalized))
        {
            return Result<string>.Failure(Error.Validation("SLUG_RESERVED", "This slug is reserved by the platform."));
        }

        return Result<string>.Success(normalized);
    }

    public static string SuggestFromName(string? name, string fallbackSuffix = "4f9a")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"clinic-{SanitizeSuffix(fallbackSuffix)}";
        }

        var builder = new StringBuilder(name.Length * 2);
        foreach (char ch in name.Trim().ToLowerInvariant())
        {
            if (ArabicTransliterationMap.TryGetValue(ch, out string? mapped))
            {
                builder.Append(mapped);
            }
            else if (ch >= '\u064B' && ch <= '\u065F')
            {
                // Strip Arabic diacritics
            }
            else if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        string collapsed = MultiHyphenRegex().Replace(builder.ToString(), "-").Trim('-');
        if (collapsed.Length > 40)
        {
            collapsed = collapsed[..40].TrimEnd('-');
        }

        if (collapsed.Length < 3 || ReservedWords.Contains(collapsed))
        {
            return $"clinic-{SanitizeSuffix(fallbackSuffix)}";
        }

        return collapsed;
    }

    private static string SanitizeSuffix(string fallbackSuffix)
    {
        string clean = NonAlphaNumRegex().Replace(fallbackSuffix.ToLowerInvariant(), "");
        return string.IsNullOrWhiteSpace(clean) ? "4f9a" : clean[..Math.Min(8, clean.Length)];
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])$")]
    private static partial Regex ValidSlugRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiHyphenRegex();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlphaNumRegex();
}

