namespace Grsms.Application.Common;

/// <summary>Turns phone numbers as people type them into the international form SMS providers expect.</summary>
public static class PhoneNumbers
{
    /// <summary>
    /// "024 123 4567" or "+233 24 123 4567" → "233241234567". A local number starting with 0 is taken as Ghanaian.
    /// Returns null when the number cannot be used.
    /// </summary>
    public static string? ToInternational(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var trimmed = phone.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (trimmed.StartsWith("00")) digits = digits[2..];
        else if (!trimmed.StartsWith('+') && digits.StartsWith('0')) digits = "233" + digits[1..];
        return digits.Length is >= 11 and <= 15 ? digits : null;
    }
}
