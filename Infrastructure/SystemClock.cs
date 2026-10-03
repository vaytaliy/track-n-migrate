namespace MailIntegrator.Infrastructure;

/// <summary>
/// Default <see cref="IClock"/> implementation backed by the operating system clock.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
