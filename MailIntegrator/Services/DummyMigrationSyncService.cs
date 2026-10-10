namespace MailIntegrator.Services;

/// <summary>
/// Simulated migration pass used until a real <see cref="ITargetMigrationService"/> is wired up.
/// </summary>
/// <remarks>
/// The 1C target integration is out of scope for this iteration, so this implementation only waits for a
/// configurable interval to let the status strip exercise its in-progress visuals. It deliberately does
/// not touch any parcel data.
/// </remarks>
public sealed class DummyMigrationSyncService : IMigrationSyncService
{
    /// <summary>
    /// The default duration of the simulated migration pass.
    /// </summary>
    public static readonly TimeSpan DefaultSimulatedDuration = TimeSpan.FromSeconds(2);

    private readonly TimeSpan _simulatedDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyMigrationSyncService"/> class using the default duration.
    /// </summary>
    public DummyMigrationSyncService()
        : this(DefaultSimulatedDuration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyMigrationSyncService"/> class.
    /// </summary>
    /// <param name="simulatedDuration">How long the fake migration should appear to take.</param>
    public DummyMigrationSyncService(TimeSpan simulatedDuration)
    {
        _simulatedDuration = simulatedDuration < TimeSpan.Zero ? TimeSpan.Zero : simulatedDuration;
    }

    /// <inheritdoc />
    // TODO: currently simulated - replace with the real 1C export driven by ITargetMigrationService.
    public Task RunAsync(CancellationToken cancellationToken) =>
        Task.Delay(_simulatedDuration, cancellationToken);
}
