namespace MailIntegrator.Infrastructure;

/// <summary>
/// Abstraction over the current time so that time dependent behaviour can be tested deterministically.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current instant expressed as Coordinated Universal Time (UTC).
    /// </summary>
    DateTime UtcNow { get; }
}
