namespace LoQi.Application.Extensions;

public static class DateExtensions
{
    public static DateTimeOffset? ParseDateTimeOffset(this string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (DateTime.TryParse(dateString, out var dateTime))
        {
            return new DateTimeOffset(dateTime);
        }

        return null;
    }
}