using MailIntegrator.Infrastructure;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// Deterministic <see cref="ILocalTimeZone"/> so display tests do not depend on the machine's zone.
/// </summary>
/// <remarks>
/// The default offset is UTC+3, which keeps the inherited display assertions readable.
/// </remarks>
public sealed class FakeLocalTimeZone : ILocalTimeZone
{
    /// <summary>
    /// The offset used when no other is supplied.
    /// </summary>
    public static readonly TimeSpan DefaultOffset = TimeSpan.FromHours(3);

    private readonly TimeSpan _offset;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeLocalTimeZone"/> class with the default offset.
    /// </summary>
    public FakeLocalTimeZone()
        : this(DefaultOffset)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeLocalTimeZone"/> class.
    /// </summary>
    /// <param name="offset">The fixed offset applied to every instant.</param>
    public FakeLocalTimeZone(TimeSpan offset)
    {
        _offset = offset;
    }

    /// <inheritdoc />
    public string DisplayName => "Test zone";

    /// <inheritdoc />
    public DateTime ToLocal(DateTime utc) => utc.Add(_offset);

    /// <inheritdoc />
    public DateTime? ToLocal(DateTime? utc) => utc?.Add(_offset);
}
