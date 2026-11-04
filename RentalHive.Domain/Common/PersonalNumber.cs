using System.Globalization;

namespace RentalHive.Domain.Common;

/// <summary>
/// Swedish personnummer / samordningsnummer, normalised to 12 digits (YYYYMMDDNNNN).
/// Accepts "YYMMDD-NNNN", "YYMMDD+NNNN" (100+ years), "YYYYMMDDNNNN" and "YYYYMMDD-NNNN".
/// </summary>
public sealed record PersonalNumber(string Value, DateOnly BirthDate)
{
    public static bool TryParse(string? input, DateOnly today, out PersonalNumber? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var raw = input.Trim();
        var centuryPlus = raw.Contains('+', StringComparison.Ordinal);
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (raw.Any(c => !char.IsAsciiDigit(c) && c is not '-' and not '+' and not ' ')) return false;

        string full;
        if (digits.Length == 12)
        {
            full = digits;
        }
        else if (digits.Length == 10)
        {
            var yy = int.Parse(digits[..2], CultureInfo.InvariantCulture);
            var century = today.Year / 100 * 100;
            if (century + yy > today.Year) century -= 100;
            if (centuryPlus) century -= 100;
            full = (century + yy).ToString(CultureInfo.InvariantCulture) + digits[2..];
        }
        else
        {
            return false;
        }

        if (!LuhnValid(full[2..])) return false;
        var year = int.Parse(full[..4], CultureInfo.InvariantCulture);
        var month = int.Parse(full[4..6], CultureInfo.InvariantCulture);
        var day = int.Parse(full[6..8], CultureInfo.InvariantCulture);
        if (day > 60) day -= 60; // samordningsnummer
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        var birth = new DateOnly(year, month, day);
        if (birth > today) return false;
        result = new PersonalNumber(full, birth);
        return true;
    }

    public static bool LuhnValid(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }

    public int AgeOn(DateOnly date)
    {
        var age = date.Year - BirthDate.Year;
        if (date < BirthDate.AddYears(age)) age--;
        return age;
    }

    /// <summary>Display form with the last four digits hidden: 19850115-****.</summary>
    public string Masked => $"{Value[..8]}-****";

    public string Formatted => $"{Value[..8]}-{Value[8..]}";

    public override string ToString() => Masked;
}
