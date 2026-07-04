namespace RetailPOS.Infrastructure.Persistence.Mapping;

internal static class UtcTime
{
    public static DateTime ToStorage(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use UTC.", parameterName);
        }

        return value.UtcDateTime;
    }

    public static DateTime? ToStorage(DateTimeOffset? value, string parameterName) =>
        value is null ? null : ToStorage(value.Value, parameterName);

    public static DateTimeOffset FromStorage(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static DateTimeOffset? FromStorage(DateTime? value) =>
        value is null ? null : FromStorage(value.Value);
}
