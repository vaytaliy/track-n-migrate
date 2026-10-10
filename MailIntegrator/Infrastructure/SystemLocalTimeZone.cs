namespace MailIntegrator.Infrastructure;

/// <summary>
/// <see cref="ILocalTimeZone"/> implementation backed by the workstation's operating system time zone.
/// </summary>
public sealed class SystemLocalTimeZone : ILocalTimeZone
{
    /// <inheritdoc />
    public string DisplayName => TimeZoneInfo.Local.DisplayName;

    /// <inheritdoc />
    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(EnsureUtcKind(utc), TimeZoneInfo.Local);

    /// <inheritdoc />
    public DateTime? ToLocal(DateTime? utc) => utc is null ? null : ToLocal(utc.Value);

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
}
