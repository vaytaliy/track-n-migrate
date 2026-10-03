namespace MailIntegrator.Services;

/// <summary>
/// Simulated synchronisation used by the application skeleton.
/// </summary>
/// <remarks>
/// External API integration is out of scope for this iteration, so this implementation only waits for a
/// configurable interval to let the status strip exercise its in-progress visuals. It deliberately does
/// not touch any parcel data.
/// </remarks>
public sealed class DummySyncService : ISyncService
{
    /// <summary>
    /// The default duration of the simulated synchronisation.
    /// </summary>
    public static readonly TimeSpan DefaultSimulatedDuration = TimeSpan.FromSeconds(2);

    private readonly TimeSpan _simulatedDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DummySyncService"/> class using the default duration.
    /// </summary>
    public DummySyncService()
        : this(DefaultSimulatedDuration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DummySyncService"/> class.
    /// </summary>
    /// <param name="simulatedDuration">How long the fake synchronisation should appear to take.</param>
    public DummySyncService(TimeSpan simulatedDuration)
    {
        _simulatedDuration = simulatedDuration < TimeSpan.Zero ? TimeSpan.Zero : simulatedDuration;
    }

    /// <inheritdoc />
    // TODO: currently simulated - replace with the real Почта России / 1C ERP calls. No parcel data is
    // modified until then, so LastStatus and LastCheckedDatetimeUtc intentionally stay empty.
    public Task RunAsync(CancellationToken cancellationToken) =>
        Task.Delay(_simulatedDuration, cancellationToken);
}
