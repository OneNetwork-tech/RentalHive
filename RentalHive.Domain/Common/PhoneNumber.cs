namespace RentalHive.Domain.Common;

/// <summary>Normalises phone numbers to E.164. Swedish national numbers (07x…) become +46…</summary>
public static class PhoneNumber
{
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (trimmed.Any(c => !char.IsAsciiDigit(c) && c is not '+' and not '-' and not ' ' and not '(' and not ')')) return false;
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (trimmed.StartsWith('+')) normalized = "+" + digits;
        else if (digits.StartsWith("00", StringComparison.Ordinal)) normalized = "+" + digits[2..];
        else if (digits.StartsWith('0')) normalized = "+46" + digits[1..];
        else return false;
        // E.164: max 15 digits; Swedish numbers have 7–9 digits after +46.
        var length = normalized.Length - 1;
        return length is >= 8 and <= 15;
    }
}
