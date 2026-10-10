namespace MailIntegrator.Infrastructure;

/// <summary>
/// Converts between Coordinated Universal Time (UTC) and Moscow time (MSK).
/// </summary>
/// <remarks>
/// Moscow has been on a fixed UTC+3 offset with no daylight saving transitions since 2014, so the
/// fallback custom time zone below is behaviourally identical to the Windows/IANA zone and keeps the
/// application working on machines where the tz database entry is unavailable.
/// </remarks>
public static class MoscowTime
{
    /// <summary>
    /// The fixed offset of Moscow time relative to UTC.
    /// </summary>
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(3);

    private static readonly TimeZoneInfo MoscowTimeZone = ResolveMoscowTimeZone();

    /// <summary>
    /// Converts a UTC instant to Moscow time.
    /// </summary>
    /// <param name="utc">The instant to convert.</param>
    /// <returns>The same instant expressed in Moscow time.</returns>
    public static DateTime ToMoscow(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(EnsureUtcKind(utc), MoscowTimeZone);

    /// <summary>
    /// Converts an optional UTC instant to Moscow time, preserving <see langword="null"/>.
    /// </summary>
    /// <param name="utc">The instant to convert, or <see langword="null"/>.</param>
    /// <returns>The converted instant, or <see langword="null"/>.</returns>
    public static DateTime? ToMoscow(DateTime? utc) => utc is null ? null : ToMoscow(utc.Value);

    /// <summary>
    /// Converts a Moscow time value to UTC.
    /// </summary>
    /// <param name="moscow">The Moscow time value to convert.</param>
    /// <returns>The same instant expressed in UTC.</returns>
    public static DateTime ToUtc(DateTime moscow) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(moscow, DateTimeKind.Unspecified), MoscowTimeZone);

    /// <summary>
    /// Converts an optional Moscow time value to UTC, preserving <see langword="null"/>.
    /// </summary>
    /// <param name="moscow">The Moscow time value to convert, or <see langword="null"/>.</param>
    /// <returns>The converted instant, or <see langword="null"/>.</returns>
    public static DateTime? ToUtc(DateTime? moscow) => moscow is null ? null : ToUtc(moscow.Value);

    /// <summary>
    /// Ensures a value carries <see cref="DateTimeKind.Utc"/> before it is handed to the time zone API.
    /// </summary>
    /// <param name="value">The value to normalise.</param>
    /// <returns>A value whose <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>.</returns>
    private static DateTime EnsureUtcKind(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>
    /// Looks up the Moscow time zone, falling back to a fixed UTC+3 zone.
    /// </summary>
    /// <returns>The resolved time zone.</returns>
    private static TimeZoneInfo ResolveMoscowTimeZone()
    {
        foreach (var timeZoneId in new[] { "Russian Standard Time", "Europe/Moscow" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the next identifier; the platform may only know one of the two naming schemes.
            }
            catch (InvalidTimeZoneException)
            {
                // The entry exists but is unusable; fall through to the fixed offset zone.
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "MSK",
            baseUtcOffset: UtcOffset,
            displayName: "Moscow Standard Time",
            standardDisplayName: "Moscow Standard Time");
    }
}
