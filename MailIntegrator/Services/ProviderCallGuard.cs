namespace MailIntegrator.Services;

/// <summary>
/// Runs a provider call and turns any failure into a reported <see cref="UserError"/> instead of an
/// exception.
/// </summary>
/// <remarks>
/// <para>
/// This is the reuse seam of the error handling: every provider call of a synchronisation pass is routed
/// through one guard, so a concrete <see cref="ITrackingService"/> (or any later target system) only
/// implements its happy path and never repeats try/catch, message building or logging.
/// </para>
/// <para>
/// A real cancellation is re-thrown, because it is not a failure: the owning pass needs it to return to the
/// idle state. An uncancelled <see cref="OperationCanceledException"/> (how a timeout surfaces) is reported
/// like any other failure.
/// </para>
/// </remarks>
public sealed class ProviderCallGuard
{
    private readonly IUserErrorLog _errorLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCallGuard"/> class.
    /// </summary>
    /// <param name="errorLog">The shared list every handled failure is reported to.</param>
    public ProviderCallGuard(IUserErrorLog errorLog) =>
        _errorLog = errorLog ?? throw new ArgumentNullException(nameof(errorLog));

    /// <summary>
    /// Runs a provider call that produces a value.
    /// </summary>
    /// <typeparam name="TResult">The value the call produces.</typeparam>
    /// <param name="providerName">The provider display name shown in the reported failure.</param>
    /// <param name="trackId">The tracking number the call belongs to, or <see langword="null"/> for a
    /// provider-level call such as the handshake.</param>
    /// <param name="call">The call to run.</param>
    /// <returns>The value the call produced, or <see langword="default"/> when the call failed.</returns>
    public async Task<TResult?> TryAsync<TResult>(
        string providerName,
        string? trackId,
        Func<Task<TResult>> call)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            return await call().ConfigureAwait(true);
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Report(providerName, trackId, exception);
            return default;
        }
    }

    /// <summary>
    /// Runs a provider call that produces no value.
    /// </summary>
    /// <param name="providerName">The provider display name shown in the reported failure.</param>
    /// <param name="trackId">The tracking number the call belongs to, or <see langword="null"/> for a
    /// provider-level call such as the handshake.</param>
    /// <param name="call">The call to run.</param>
    /// <returns><see langword="true"/> when the call succeeded, otherwise <see langword="false"/>.</returns>
    public async Task<bool> TryAsync(string providerName, string? trackId, Func<Task> call)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            await call().ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Report(providerName, trackId, exception);
            return false;
        }
    }

    /// <summary>
    /// Reports a handled failure that was not raised by a guarded call, for example an unroutable parcel.
    /// </summary>
    /// <param name="providerName">The provider display name, or the provider code when it is unknown.</param>
    /// <param name="trackId">The tracking number, or <see langword="null"/> for a provider-level failure.</param>
    /// <param name="exception">The handled exception.</param>
    public void Report(string providerName, string? trackId, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(exception);

        _errorLog.Report(new UserError(providerName, trackId, TrackingFailureDescriber.Describe(exception)));
    }
}
