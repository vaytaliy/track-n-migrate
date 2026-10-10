using System.Data;
using System.Globalization;
using Dapper;
using MailIntegrator.Models;

namespace MailIntegrator.Data;

/// <summary>
/// Teaches Dapper to persist <see cref="ParcelStatus"/> as its enum name and to materialise it back.
/// </summary>
/// <remarks>
/// Storing the name keeps the database readable and survives enum member reordering. Unrecognised text
/// degrades to <see cref="ParcelStatus.Unknown"/> rather than throwing, so a value written by an older
/// build can never break a query. Dapper resolves nullable properties through the same handler.
/// </remarks>
public sealed class ParcelStatusTypeHandler : SqlMapper.TypeHandler<ParcelStatus>
{
    /// <inheritdoc />
    public override ParcelStatus Parse(object value)
    {
        switch (value)
        {
            case string text when Enum.TryParse<ParcelStatus>(text.Trim(), ignoreCase: true, out var parsed)
                                  && Enum.IsDefined(parsed):
                return parsed;

            case int or long or short or byte:
                var raw = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return Enum.IsDefined((ParcelStatus)raw) ? (ParcelStatus)raw : ParcelStatus.Unknown;

            default:
                return ParcelStatus.Unknown;
        }
    }

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, ParcelStatus value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = value.ToString();
    }
}
