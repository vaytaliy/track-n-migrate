using System.Net.Http;
using System.Net.Sockets;

namespace MailIntegrator.Services;

/// <summary>
/// Turns a failed provider call into the short Russian reason shown to the operator.
/// </summary>
/// <remarks>
/// <para>
/// This is the single place that knows how an exception is explained to the operator, so a concrete
/// provider never writes error text: it either lets the raw exception bubble up and gets the default
/// wording, or throws <see cref="SyncException"/> with its own reason-only sentence when it can be more
/// precise.
/// </para>
/// <para>
/// The message of a general exception is deliberately never copied: it may carry a provider payload or a
/// credential. Only <see cref="SyncException"/> is trusted, because its contract already forbids secret
/// material.
/// </para>
/// </remarks>
public static class TrackingFailureDescriber
{
    /// <summary>The reason used when the provider rejected the credentials.</summary>
    public const string RejectedCredentials = "служба отклонила учётные данные";

    /// <summary>The reason used when the provider could not be reached or answered with a transport error.</summary>
    public const string UnreachableService = "не удалось установить соединение со службой";

    /// <summary>The reason used when the provider did not answer in time.</summary>
    public const string TimedOut = "служба не ответила вовремя";

    /// <summary>The reason used for anything that is not recognised.</summary>
    public const string UnexpectedFailure = "непредвиденная ошибка при обращении к службе";

    /// <summary>
    /// Describes a failure of a provider call.
    /// </summary>
    /// <param name="exception">The exception the provider call raised.</param>
    /// <returns>The concise Russian reason, safe to display to the operator.</returns>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            // A provider that knows better supplies its own reason-only sentence.
            SyncException sync => sync.Message,
            UnauthorizedAccessException => RejectedCredentials,
            HttpRequestException { StatusCode: { } statusCode } =>
                $"служба ответила ошибкой {(int)statusCode}",
            HttpRequestException => UnreachableService,
            // An uncancelled cancellation is how a timeout surfaces; the guard re-throws real cancellations.
            OperationCanceledException or TimeoutException => TimedOut,
            SocketException => UnreachableService,
            _ => UnexpectedFailure,
        };
    }
}
