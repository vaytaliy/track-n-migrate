using System.Globalization;
using System.Text.Json;

namespace MailIntegrator.Services;

/// <summary>
/// In-process stand-in for the Почта России tracking API.
/// </summary>
/// <remarks>
/// <para>
/// The mock replaces only the network leg of a real integration. It validates the hardcoded basic-auth
/// credentials, issues <see cref="MockAccessToken"/> and answers the documented trace URL with a scripted
/// payload. The request URL is still built exactly as the real API expects
/// (<c>GET https://tracking.pochta.ru/1.0/trace?barcode=&lt;trackid&gt;</c>), so the request shape can be
/// verified and a real client would not change the caller.
/// </para>
/// <para>
/// A rejected handshake, or a trace call without the issued token, raises
/// <see cref="UnauthorizedAccessException"/>, which mirrors the HTTP 401 the real endpoint answers with.
/// </para>
/// </remarks>
public sealed class PochtaRussiaMockEndpoint
{
    /// <summary>The root of the real API this mock stands in for.</summary>
    public const string BaseUrl = "https://tracking.pochta.ru";

    /// <summary>The trace path template; the single placeholder takes the URL-encoded tracking number.</summary>
    public const string TracePathTemplate = "/1.0/trace?barcode={0}";

    /// <summary>The login the mocked handshake accepts.</summary>
    public const string MockUser = "mock_user";

    /// <summary>The password the mocked handshake accepts.</summary>
    public const string MockPassword = "mock_pw";

    /// <summary>The access token the mocked handshake issues.</summary>
    public const string MockAccessToken = "access_token";

    private const string BarcodePlaceholder = "%BARCODE%";

    /// <summary>
    /// The scripted trace body. It is the exact payload documented for the mocked endpoint and keeps the
    /// provider offsets, so the parsing and mapping code under test sees the production format.
    /// </summary>
    private const string TraceResponseTemplate = """
        {
          "barcode": %BARCODE%,
          "history": [
            {
              "operation-type": "Принято в отделении связи",
              "operation-attribute": "Единичная",
              "operation-date": "2026-10-01T09:00:00.000+03:00",
              "index": "101000",
              "city": "Москва"
            },
            {
              "operation-type": "Обработка",
              "operation-attribute": "Прибыло в сортировочный центр",
              "operation-date": "2026-10-01T22:15:00.000+03:00",
              "index": "140960",
              "city": "Подольск"
            },
            {
              "operation-type": "Обработка",
              "operation-attribute": "Прибыло в место вручения",
              "operation-date": "2026-10-03T11:20:00.000+03:00",
              "index": "190000",
              "city": "Санкт-Петербург"
            },
            {
              "operation-type": "Вручение",
              "operation-attribute": "Вручение адресату",
              "operation-date": "2026-10-03T16:45:00.000+03:00",
              "index": "190000",
              "city": "Санкт-Петербург"
            }
          ]
        }
        """;

    /// <summary>
    /// Gets the URL of the most recent simulated trace request, so callers and tests can pin the request shape.
    /// </summary>
    public Uri? LastTraceRequestUri { get; private set; }

    /// <summary>
    /// Simulates the basic-auth handshake: only the hardcoded mock credentials are accepted.
    /// </summary>
    /// <param name="user">The login supplied by the operator.</param>
    /// <param name="password">The password supplied by the operator.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see cref="MockAccessToken"/> when the credentials match.</returns>
    /// <exception cref="UnauthorizedAccessException">The credentials do not match the mocked account.</exception>
    public Task<string> AuthenticateAsync(string user, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var credentialsMatch =
            string.Equals(user, MockUser, StringComparison.Ordinal) &&
            string.Equals(password, MockPassword, StringComparison.Ordinal);

        if (!credentialsMatch)
        {
            throw new UnauthorizedAccessException(
                $"Служба «Почта России» отклонила учётные данные: ожидаются логин «{MockUser}» и пароль «{MockPassword}».");
        }

        return Task.FromResult(MockAccessToken);
    }

    /// <summary>
    /// Simulates <c>GET /1.0/trace?barcode=&lt;trackid&gt;</c>, guarded by the access token.
    /// </summary>
    /// <param name="accessToken">The token issued by <see cref="AuthenticateAsync"/>.</param>
    /// <param name="trackId">The tracking number to trace.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The scripted response body.</returns>
    /// <exception cref="UnauthorizedAccessException">The token is missing or is not the issued token.</exception>
    public async Task<string> GetTraceAsync(
        string? accessToken,
        string trackId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(accessToken, MockAccessToken, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Служба «Почта России» отклонила запрос: не передан действующий токен доступа.");
        }

        LastTraceRequestUri = BuildTraceRequestUri(trackId);

        // A real client would await the HTTP response here. The mock yields instead, so callers see the
        // same asynchronous shape without opening a socket.
        await Task.Yield();

        return TraceResponseTemplate.Replace(
            BarcodePlaceholder,
            JsonSerializer.Serialize(trackId),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the absolute trace URL of a tracking number, exactly as the real API expects it.
    /// </summary>
    /// <param name="trackId">The tracking number to place in the query string.</param>
    /// <returns>The absolute request URL.</returns>
    /// <exception cref="ArgumentException">The tracking number is blank.</exception>
    public static Uri BuildTraceRequestUri(string trackId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);

        var path = string.Format(CultureInfo.InvariantCulture, TracePathTemplate, Uri.EscapeDataString(trackId));
        return new Uri(new Uri(BaseUrl), path);
    }
}
