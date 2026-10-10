using MailIntegrator.Configuration;
using MailIntegrator.Services;

namespace MailIntegrator.Tests.Unit.Services.Tracking;

/// <summary>
/// Covers the optional tracking-page part of the <see cref="ITrackingService"/> contract: a provider either
/// publishes a URL for a tracking number or keeps the default "no link" answer.
/// </summary>
public sealed class TrackingServiceContractTests
{
    [Fact]
    public void A_provider_without_a_page_keeps_the_default_no_link_answer()
    {
        ITrackingService service = new DummyTrackingService(new TrackingServiceDescriptor("DHL", "DHL"));

        Assert.Null(service.GetTrackingUrl("RU1"));
    }

    [Fact]
    public void PochtaRussia_publishes_its_public_tracking_page()
    {
        ITrackingService service = new PochtaRussiaTrackingService(StatusMappingCatalog.Default);

        Assert.Equal(
            "https://www.pochta.ru/tracking#RU4729103852CN",
            service.GetTrackingUrl("RU4729103852CN")!.AbsoluteUri);
    }

    [Fact]
    public void PochtaRussia_escapes_the_tracking_number()
    {
        ITrackingService service = new PochtaRussiaTrackingService(StatusMappingCatalog.Default);

        Assert.Equal(
            "https://www.pochta.ru/tracking#RU%20123",
            service.GetTrackingUrl("RU 123")!.AbsoluteUri);
    }
}
