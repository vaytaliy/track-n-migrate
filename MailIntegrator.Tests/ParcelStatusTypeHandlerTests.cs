using System.Data;
using MailIntegrator.Data;
using MailIntegrator.Models;
using Microsoft.Data.Sqlite;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the enum to storage conversion, including the graceful handling of unmapped values.
/// </summary>
public sealed class ParcelStatusTypeHandlerTests
{
    private readonly ParcelStatusTypeHandler _handler = new();

    [Theory]
    [InlineData("Delivered", ParcelStatus.Delivered)]
    [InlineData("delivered", ParcelStatus.Delivered)]
    [InlineData("  OutForDelivery  ", ParcelStatus.OutForDelivery)]
    public void Parse_reads_an_enum_name_case_insensitively(string stored, ParcelStatus expected)
    {
        Assert.Equal(expected, _handler.Parse(stored));
    }

    [Theory]
    [InlineData("Неизвестно")]
    [InlineData("")]
    [InlineData("42")]
    public void Parse_degrades_an_unrecognised_value_to_unknown(string stored)
    {
        Assert.Equal(ParcelStatus.Unknown, _handler.Parse(stored));
    }

    [Fact]
    public void Parse_reads_a_numeric_value_when_it_is_a_defined_member()
    {
        Assert.Equal(ParcelStatus.Delivered, _handler.Parse((int)ParcelStatus.Delivered));
        Assert.Equal(ParcelStatus.Delivered, _handler.Parse((long)ParcelStatus.Delivered));
        Assert.Equal(ParcelStatus.Unknown, _handler.Parse(999));
    }

    [Fact]
    public void SetValue_writes_the_enum_name_as_a_string()
    {
        var parameter = new SqliteParameter();

        _handler.SetValue(parameter, ParcelStatus.OutForDelivery);

        Assert.Equal(DbType.String, parameter.DbType);
        Assert.Equal("OutForDelivery", parameter.Value);
    }
}
