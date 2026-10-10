using System.Globalization;

namespace MailIntegrator.Data;

/// <summary>
/// Converts <see cref="DateTime"/> values to and from the ISO-8601 UTC text format used in SQLite.
/// </summary>
/// <remarks>
/// The explicit trailing <c>Z</c> makes the stored kind unambiguous, which matters because display
/// converts to the local time zone and must never guess an offset when reading back.
/// </remarks>
public static class DateTimeStorage
{
    private const string StorageFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    /// <summary>
    /// Formats an optional instant for storage, normalising it to UTC first.
    /// </summary>
    /// <param name="value">The instant to format, or <see langword="null"/>.</param>
    /// <returns>The formatted value, or <see langword="null"/> when the input was <see langword="null"/>.</returns>
    public static string? ToStorage(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        var utc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };

        return utc.ToString(StorageFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an instant previously written by <see cref="ToStorage(DateTime?)"/>.
    /// </summary>
    /// <param name="value">The raw column value.</param>
    /// <returns>The parsed UTC instant, or <see langword="null"/> when the column was empty.</returns>
    /// <exception cref="FormatException">The stored text is not in the expected format.</exception>
    public static DateTime? FromStorage(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.ParseExact(
            text,
            StorageFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
