using System.Text;

namespace Core.Domain;

/// <summary>
/// Normalizes Arabic and Latin patient names and phone numbers for deterministic duplicate detection and search.
/// </summary>
public static class ArabicNormalizer
{
    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        bool lastWasSpace = true;

        foreach (char ch in input.Trim())
        {
            if (IsArabicDiacriticOrTatweel(ch))
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            char normalized = ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                'ة' => 'ه',
                'ؤ' => 'و',
                'ئ' => 'ي',
                _ => NormalizeDigitOrLatin(ch)
            };

            sb.Append(normalized);
            lastWasSpace = false;
        }

        return sb.ToString().Trim();
    }

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(phone.Length);
        foreach (char ch in phone)
        {
            char mapped = MapArabicIndicDigit(ch);
            if (char.IsAsciiDigit(mapped))
            {
                sb.Append(mapped);
            }
        }

        string digits = sb.ToString();
        if (digits.StartsWith("0020", StringComparison.Ordinal) && digits.Length > 4)
        {
            digits = "0" + digits[4..];
        }
        else if (digits.StartsWith("20", StringComparison.Ordinal) && digits.Length == 12)
        {
            digits = "0" + digits[2..];
        }

        return digits;
    }

    private static bool IsArabicDiacriticOrTatweel(char ch)
    {
        return ch == '\u0640' ||
               (ch >= '\u064B' && ch <= '\u065F') ||
               ch == '\u0670' ||
               (ch >= '\u06D6' && ch <= '\u06ED');
    }

    private static char NormalizeDigitOrLatin(char ch)
    {
        char digit = MapArabicIndicDigit(ch);
        return digit != ch ? digit : char.ToLowerInvariant(ch);
    }

    private static char MapArabicIndicDigit(char ch)
    {
        return ch switch
        {
            >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
            >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
            _ => ch
        };
    }
}

