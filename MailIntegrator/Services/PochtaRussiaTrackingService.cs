using MailIntegrator.Configuration;
using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// The Почта России tracking provider, backed by the simulated API endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="DummyTrackingService"/>, this provider reproduces the real integration up to the
/// network boundary: it performs the basic-auth handshake, remembers the access token the endpoint issued,
/// polls the trace endpoint and adapts the provider payload onto <see cref="TrackingResult"/>. Only the
/// socket call is replaced by <see cref="PochtaRussiaMockEndpoint"/>.
/// </para>
/// <para>
/// The handshake accepts only the hardcoded mock account (<c>mock_user</c> / <c>mock_pw</c>, entered in the
/// settings dialog) and issues <c>access_token</c>; any other credentials, or a trace call before a
/// successful handshake, raise <see cref="UnauthorizedAccessException"/>.
/// </para>
/// </remarks>
public sealed class PochtaRussiaTrackingService : ITrackingService
{
    /// <summary>The stable provider code stored on a parcel and used as the credential key.</summary>
    public const string Code = "PochtaRussia";

    /// <summary>The human readable provider name shown in the grid and the settings dialog.</summary>
    public const string DisplayName = "Почта России";

    /// <summary>The root of the provider's public tracking page.</summary>
    public const string PublicTrackingUrl = "https://www.pochta.ru/tracking";

    private readonly PochtaRussiaMockEndpoint _endpoint;
    private readonly StatusMappingCatalog _statusMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="PochtaRussiaTrackingService"/> class over a fresh mock endpoint.
    /// </summary>
    /// <param name="statusMappings">The configured mapping of provider operation types onto generic statuses.</param>
    public PochtaRussiaTrackingService(StatusMappingCatalog statusMappings)
        : this(new PochtaRussiaMockEndpoint(), statusMappings)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PochtaRussiaTrackingService"/> class.
    /// </summary>
    /// <param name="endpoint">The simulated endpoint that answers the provider requests.</param>
    /// <param name="statusMappings">The configured mapping of provider operation types onto generic statuses.</param>
    public PochtaRussiaTrackingService(PochtaRussiaMockEndpoint endpoint, StatusMappingCatalog statusMappings)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _statusMappings = statusMappings ?? throw new ArgumentNullException(nameof(statusMappings));
        Descriptor = new TrackingServiceDescriptor(Code, DisplayName);
    }

    /// <inheritdoc />
    public TrackingServiceDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the access token issued by the last successful handshake, or <see langword="null"/> when the
    /// provider has not authenticated yet.
    /// </summary>
    public string? CurrentAccessToken { get; private set; }

    /// <inheritdoc />
    /// <exception cref="UnauthorizedAccessException">The credentials do not match the mocked account.</exception>
    public async Task<string> AuthenticateBasicAsync(
        string user,
        string password,
        CancellationToken cancellationToken)
    {
        var accessToken = await _endpoint.AuthenticateAsync(user, password, cancellationToken);
        CurrentAccessToken = accessToken;
        return accessToken;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The latest history record wins: its <c>operation-date</c> becomes
    /// <see cref="TrackingResult.StatusDatetimeUtc"/> (converted from the provider offset to UTC) and its
    /// <c>operation-type</c> is mapped through the configured <see cref="StatusMappingCatalog"/>.
    /// </remarks>
    /// <exception cref="UnauthorizedAccessException">No access token was issued before the call.</exception>
    /// <exception cref="SyncException">The payload carried no operation history.</exception>
    public async Task<TrackingResult> TrackParcelAsync(string trackId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

        var responseBody = await _endpoint.GetTraceAsync(CurrentAccessToken, trackId, cancellationToken);
        var response = PochtaRussiaTraceResponse.Parse(responseBody);
        var latestEntry = response.FindLatestEntry()
            ?? throw new SyncException("не вернула историю операций");

        var status = _statusMappings.MapStatus(Code, latestEntry.OperationType);

        return new TrackingResult(trackId, status, latestEntry.OperationDateUtc);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The tracking number is placed in the URL fragment, which is how the public page accepts it, so the
    /// operator lands on the provider's own page with the number already filled in.
    /// </remarks>
    public Uri? GetTrackingUrl(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            return null;
        }

        return new Uri($"{PublicTrackingUrl}#{Uri.EscapeDataString(trackId)}");
    }
}
