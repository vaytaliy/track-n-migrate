namespace MailIntegrator.Infrastructure;

/// <summary>
/// Converts UTC instants to the local time zone used for display.
/// </summary>
/// <remarks>
/// An abstraction rather than a direct <see cref="TimeZoneInfo.Local"/> call, so view model tests can
/// pin a zone and stay independent of the machine the tests run on.
/// </remarks>
public interface ILocalTimeZone
{
    /// <summary>
    /// Gets the human readable name of the zone, for diagnostics.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Converts a UTC instant to local time.
    /// </summary>
    /// <param name="utc">The instant to convert.</param>
    /// <returns>The same instant expressed in local time.</returns>
    DateTime ToLocal(DateTime utc);

    /// <summary>
    /// Converts an optional UTC instant to local time, preserving <see langword="null"/>.
    /// </summary>
    /// <param name="utc">The instant to convert, or <see langword="null"/>.</param>
    /// <returns>The converted instant, or <see langword="null"/>.</returns>
    DateTime? ToLocal(DateTime? utc);
}
