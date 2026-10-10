using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;

namespace MailIntegrator.Tests.Unit.Services.Tracking;

/// <summary>
/// Covers provider registration, lookup and misconfiguration handling.
/// </summary>
public sealed class TrackingServiceRegistryTests
{
    [Fact]
    public void Services_preserves_the_registration_order()
    {
        var registry = CreateRegistry();

        Assert.Equal(["PochtaRussia", "DHL"], registry.Services.Select(service => service.Descriptor.Code));
    }

    [Fact]
    public void FindByCode_is_case_insensitive_and_trims_the_candidate()
    {
        var registry = CreateRegistry();
        var dhl = registry.Services[1];

        Assert.Same(dhl, registry.FindByCode("DHL"));
        Assert.Same(dhl, registry.FindByCode("dhl"));
        Assert.Same(dhl, registry.FindByCode("  DHL  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UnknownCarrier")]
    public void FindByCode_returns_null_for_an_unusable_candidate(string? code)
    {
        Assert.Null(CreateRegistry().FindByCode(code));
    }

    [Fact]
    public void ContainsCode_reports_whether_a_code_is_registered()
    {
        var registry = CreateRegistry();

        Assert.True(registry.ContainsCode("dhl"));
        Assert.False(registry.ContainsCode("UnknownCarrier"));
        Assert.False(registry.ContainsCode(null));
    }

    [Fact]
    public void A_blank_provider_code_is_rejected_at_registration()
    {
        var services = new[] { new FakeTrackingService("  ", "Без кода") };

        Assert.Throws<ArgumentException>(() => new TrackingServiceRegistry(services));
    }

    [Fact]
    public void A_duplicate_provider_code_is_rejected_at_registration_regardless_of_case()
    {
        var services = new[]
        {
            new FakeTrackingService("DHL", "DHL"),
            new FakeTrackingService("dhl", "DHL duplicate"),
        };

        Assert.Throws<ArgumentException>(() => new TrackingServiceRegistry(services));
    }

    /// <summary>Creates a registry with two providers.</summary>
    private static TrackingServiceRegistry CreateRegistry() =>
        new(
        [
            new FakeTrackingService("PochtaRussia", "Почта России"),
            new FakeTrackingService("DHL", "DHL"),
        ]);
}
