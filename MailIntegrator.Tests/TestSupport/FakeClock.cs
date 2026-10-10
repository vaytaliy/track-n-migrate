using MailIntegrator.Infrastructure;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// Deterministic clock so creation timestamps can be asserted exactly.
/// </summary>
public sealed class FakeClock : IClock
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeClock"/> class.
    /// </summary>
    /// <param name="utcNow">The instant the clock starts at.</param>
    public FakeClock(DateTime utcNow)
    {
        UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public DateTime UtcNow { get; set; }

    /// <summary>
    /// Advances the clock by the given amount.
    /// </summary>
    /// <param name="amount">The amount of time to advance.</param>
    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}
