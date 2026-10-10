using System.Data;
using Dapper;

namespace MailIntegrator.Data;

/// <summary>
/// Teaches Dapper to persist and materialise <see cref="DateTime"/> values as explicit UTC text.
/// </summary>
/// <remarks>
/// Registering this handler globally means entities can expose plain <see cref="DateTime"/> properties
/// without every repository repeating the storage conversion. Dapper resolves nullable properties
/// through the same handler.
/// </remarks>
public sealed class UtcDateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    /// <inheritdoc />
    public override DateTime Parse(object value) =>
        DateTimeStorage.FromStorage(value)
        ?? throw new DataException("A non-null DateTime column produced no value.");

    /// <inheritdoc />
    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = DateTimeStorage.ToStorage(value) ?? (object)DBNull.Value;
    }
}
