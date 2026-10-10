using MailIntegrator.Configuration;
using MailIntegrator.Models;
using MailIntegrator.Services;

namespace MailIntegrator.Tests.Unit.Services.Tracking;

/// <summary>
/// Covers the scripted Почта России provider: handshake, token guard, request shape, payload parsing and
/// the mapping of the latest operation onto the generic result.
/// </summary>
public sealed class PochtaRussiaTrackingServiceTests
{
    private const string TrackId = "RU4729103852CN";

    [Fact]
    public async Task AuthenticateBasicAsync_accepts_the_hardcoded_mock_credentials()
    {
        var service = new PochtaRussiaTrackingService(StatusMappingCatalog.Default);

        var token = await service.AuthenticateBasicAsync(
            PochtaRussiaMockEndpoint.MockUser,
            PochtaRussiaMockEndpoint.MockPassword,
            CancellationToken.None);

        Assert.Equal("access_token", token);
        Assert.Equal("access_token", service.CurrentAccessToken);
    }

    [Theory]
    [InlineData("user", "mock_pw")]
    [InlineData("mock_user", "pw")]
    [InlineData("mock_user", "MOCK_PW")]
    [InlineData("", "")]
    public async Task AuthenticateBasicAsync_rejects_any_other_credentials(string user, string password)
    {
        var service = new PochtaRussiaTrackingService(StatusMappingCatalog.Default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.AuthenticateBasicAsync(user, password, CancellationToken.None));

        Assert.Null(service.CurrentAccessToken);
    }

    [Fact]
    public async Task TrackParcelAsync_requires_a_token_issued_by_the_handshake()
    {
        var service = new PochtaRussiaTrackingService(StatusMappingCatalog.Default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.TrackParcelAsync(TrackId, CancellationToken.None));
    }

    [Fact]
    public async Task TrackParcelAsync_returns_the_latest_operation_as_delivered_in_utc()
    {
        var service = await CreateAuthenticatedServiceAsync();

        var result = await service.TrackParcelAsync(TrackId, CancellationToken.None);

        // The scripted history ends with "Вручение" at 2026-10-03T16:45:00+03:00.
        Assert.Equal(TrackId, result.TrackingId);
        Assert.Equal(ParcelStatus.Delivered, result.CurrentStatus);
        Assert.Equal(new DateTime(2026, 10, 3, 13, 45, 0, DateTimeKind.Utc), result.StatusDatetimeUtc);
        Assert.Equal(DateTimeKind.Utc, result.StatusDatetimeUtc.Kind);
    }

    [Fact]
    public async Task TrackParcelAsync_polls_the_documented_trace_url()
    {
        var endpoint = new PochtaRussiaMockEndpoint();
        var service = new PochtaRussiaTrackingService(endpoint, StatusMappingCatalog.Default);
        await service.AuthenticateBasicAsync(
            PochtaRussiaMockEndpoint.MockUser,
            PochtaRussiaMockEndpoint.MockPassword,
            CancellationToken.None);

        await service.TrackParcelAsync(TrackId, CancellationToken.None);

        Assert.Equal(
            "https://tracking.pochta.ru/1.0/trace?barcode=RU4729103852CN",
            endpoint.LastTraceRequestUri!.ToString());
    }

    [Fact]
    public async Task TrackParcelAsync_honours_cancellation()
    {
        var service = await CreateAuthenticatedServiceAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.TrackParcelAsync(TrackId, cancellation.Token));
    }

    /// <summary>
    /// Verifies that the mapping really comes from the configuration instead of a hardcoded table: the
    /// scripted <c>Вручение</c> operation is remapped to <see cref="ParcelStatus.Processing"/>.
    /// </summary>
    [Fact]
    public async Task TrackParcelAsync_uses_the_configured_status_mapping()
    {
        var config = new AppConfig
        {
            StatusMappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["PochtaRussia"] = new() { ["Вручение"] = "Processing" },
            },
        };
        var service = new PochtaRussiaTrackingService(new StatusMappingCatalog(config));
        await service.AuthenticateBasicAsync(
            PochtaRussiaMockEndpoint.MockUser,
            PochtaRussiaMockEndpoint.MockPassword,
            CancellationToken.None);

        var result = await service.TrackParcelAsync(TrackId, CancellationToken.None);

        Assert.Equal(ParcelStatus.Processing, result.CurrentStatus);
    }

    /// <summary>Creates a provider that has already completed the mock handshake.</summary>
    private static async Task<PochtaRussiaTrackingService> CreateAuthenticatedServiceAsync()
    {
        var service = new PochtaRussiaTrackingService(StatusMappingCatalog.Default);
        await service.AuthenticateBasicAsync(
            PochtaRussiaMockEndpoint.MockUser,
            PochtaRussiaMockEndpoint.MockPassword,
            CancellationToken.None);

        return service;
    }
}
