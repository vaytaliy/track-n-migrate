using MailIntegrator.Models;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the single mapping between <see cref="ParcelStatus"/> and its presentation label.
/// </summary>
public sealed class ParcelStatusExtensionsTests
{
    [Theory]
    [InlineData(ParcelStatus.Unknown, "?")]
    [InlineData(ParcelStatus.Processing, "В процессе")]
    [InlineData(ParcelStatus.InTransit, "Транзит")]
    [InlineData(ParcelStatus.Customs, "Таможня")]
    [InlineData(ParcelStatus.OutForDelivery, "Доставляется")]
    [InlineData(ParcelStatus.Exception, "Ошибка")]
    [InlineData(ParcelStatus.Delivered, "Доставлено")]
    public void ToDisplayLabel_maps_every_member(ParcelStatus status, string expected)
    {
        Assert.Equal(expected, status.ToDisplayLabel());
    }

    [Theory]
    [InlineData(ParcelStatus.Delivered, true)]
    [InlineData(ParcelStatus.Exception, true)]
    [InlineData(ParcelStatus.Unknown, false)]
    [InlineData(ParcelStatus.Processing, false)]
    [InlineData(ParcelStatus.InTransit, false)]
    [InlineData(ParcelStatus.Customs, false)]
    [InlineData(ParcelStatus.OutForDelivery, false)]
    public void IsFinal_only_accepts_delivered_and_exception(ParcelStatus status, bool expected)
    {
        Assert.Equal(expected, status.IsFinal());
    }

    [Fact]
    public void Unknown_is_the_default_value_so_an_unmapped_status_is_safe()
    {
        Assert.Equal(ParcelStatus.Unknown, default(ParcelStatus));
        Assert.Equal("?", default(ParcelStatus).ToDisplayLabel());
    }
}
