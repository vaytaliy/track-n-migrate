using System.IO;
using MailIntegrator.Configuration;
using MailIntegrator.Models;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the configuration driven provider status mapping.
/// </summary>
public sealed class StatusMappingCatalogTests
{
    [Theory]
    [InlineData("Прием", ParcelStatus.Processing)]
    [InlineData("Передано курьеру", ParcelStatus.OutForDelivery)]
    [InlineData("Неудачная попытка вручения", ParcelStatus.Exception)]
    [InlineData("Вручение", ParcelStatus.Delivered)]
    [InlineData("Возврат", ParcelStatus.Delivered)]
    [InlineData("Вручение отправителю", ParcelStatus.Delivered)]
    [InlineData("  Вручение  ", ParcelStatus.Delivered)]
    public void Default_mapping_follows_the_provider_table(string operationType, ParcelStatus expected)
    {
        Assert.Equal(expected, StatusMappingCatalog.Default.MapStatus("PochtaRussia", operationType));
    }

    [Theory]
    [InlineData("Обработка")]
    [InlineData("Принято в отделении связи")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MapStatus_falls_back_to_unknown_for_an_unmapped_operation_type(string? operationType)
    {
        Assert.Equal(ParcelStatus.Unknown, StatusMappingCatalog.Default.MapStatus("PochtaRussia", operationType));
    }

    [Theory]
    [InlineData("UnknownCarrier", "Вручение")]
    [InlineData("DHL", "Вручение")]
    [InlineData(null, "Вручение")]
    [InlineData("", "Вручение")]
    public void MapStatus_is_unknown_for_a_service_without_mappings(string? serviceCode, string operationType)
    {
        Assert.Equal(ParcelStatus.Unknown, StatusMappingCatalog.Default.MapStatus(serviceCode, operationType));
    }

    [Fact]
    public void MapStatus_reads_a_customized_mapping_case_insensitively()
    {
        var catalog = new StatusMappingCatalog(new AppConfig
        {
            StatusMappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["custom"] = new() { ["delivered abroad"] = "Delivered" },
            },
        });

        Assert.Equal(ParcelStatus.Delivered, catalog.MapStatus("CUSTOM", "Delivered Abroad"));
    }

    [Fact]
    public void An_unknown_target_status_fails_fast_with_the_offending_entry()
    {
        var config = new AppConfig
        {
            StatusMappings = new Dictionary<string, Dictionary<string, string>>
            {
                ["PochtaRussia"] = new() { ["Вручение"] = "Delivred" },
            },
        };

        var exception = Assert.Throws<InvalidDataException>(() => new StatusMappingCatalog(config));

        Assert.Contains("Вручение", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Delivred", exception.Message, StringComparison.Ordinal);
    }
}
