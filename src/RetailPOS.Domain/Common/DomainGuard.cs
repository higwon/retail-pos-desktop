namespace RetailPOS.Domain.Common;

internal static class DomainGuard
{
    public static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }

    public static decimal Money(decimal value, string parameterName, bool allowZero = true)
    {
        if (value < 0 || (!allowZero && value == 0) || decimal.Truncate(value) != value)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Money must be a whole, non-negative KRW amount.");
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC.", parameterName);
        }

        return value;
    }
}
