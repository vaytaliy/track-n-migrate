namespace MailIntegrator.Services;

/// <summary>
/// Raised when a synchronisation pass cannot run but the cause is expected and reportable to the operator.
/// </summary>
/// <remarks>
/// The message is safe to display: it must never contain secret material or raw HTTP payloads.
/// </remarks>
public sealed class SyncException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncException"/> class.
    /// </summary>
    /// <param name="message">The user facing description of the failure.</param>
    public SyncException(string message)
        : base(message)
    {
    }
}
