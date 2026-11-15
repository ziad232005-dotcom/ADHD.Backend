using System.Text;
using System.Text.RegularExpressions;

namespace ADHD.Backend.Sms;

/// <summary>Converts phone numbers typed by users to the E.164 format.</summary>
public static partial class PhoneNumber
{
    private const string EgyptCountryCode = "20";

    // Egyptian mobile numbers: +20 followed by 10, 11, 12 or 15 and 8 digits.
    [GeneratedRegex(@"^\+201[0125]\d{8}$")]
    private static partial Regex EgyptMobile();

    /// <summary>
    /// Accepts forms such as 01012345678, 1012345678, 201012345678,
    /// 00201012345678 and +201012345678 (Arabic-Indic digits are allowed).
    /// </summary>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var cleaned = KeepDigitsAndPlus(input);

        if (cleaned.Count(c => c == '+') > 1 ||
            (cleaned.Contains('+') && !cleaned.StartsWith('+')))
        {
            return false;
        }

        string e164;

        if (cleaned.StartsWith('+'))
        {
            e164 = cleaned;
        }
        else if (cleaned.StartsWith("00"))
        {
            e164 = "+" + cleaned[2..];
        }
        else if (cleaned.StartsWith(EgyptCountryCode) && cleaned.Length == 12)
        {
            e164 = "+" + cleaned;
        }
        else if (cleaned.StartsWith('0'))
        {
            e164 = "+" + EgyptCountryCode + cleaned[1..];
        }
        else if (cleaned.StartsWith('1') && cleaned.Length == 10)
        {
            e164 = "+" + EgyptCountryCode + cleaned;
        }
        else
        {
            return false;
        }

        var digitCount = e164.Length - 1;

        if (digitCount < 8 || digitCount > 15)
        {
            return false;
        }

        if (e164.StartsWith("+" + EgyptCountryCode) &&
            !EgyptMobile().IsMatch(e164))
        {
            return false;
        }

        normalized = e164;
        return true;
    }

    private static string KeepDigitsAndPlus(string input)
    {
        var builder = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (c >= '٠' && c <= '٩')
            {
                // Arabic-Indic digit to its ASCII equivalent.
                builder.Append((char)('0' + (c - '٠')));
            }
            else if (char.IsAsciiDigit(c) || c == '+')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
