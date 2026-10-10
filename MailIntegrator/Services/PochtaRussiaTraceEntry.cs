using System.Text.Json.Serialization;

namespace MailIntegrator.Services;

/// <summary>
/// One operation record of the Почта России trace history.
/// </summary>
/// <param name="OperationType">The provider operation type, mapped onto <see cref="Models.ParcelStatus"/>.</param>
/// <param name="OperationAttribute">The provider free-text refinement of the operation.</param>
/// <param name="OperationDate">The instant of the operation, including the provider time zone offset.</param>
/// <param name="Index">The postal index of the office where the operation was registered.</param>
/// <param name="City">The city of the office where the operation was registered.</param>
public sealed record PochtaRussiaTraceEntry(
    [property: JsonPropertyName("operation-type")] string OperationType,
    [property: JsonPropertyName("operation-attribute")] string OperationAttribute,
    [property: JsonPropertyName("operation-date")] DateTimeOffset OperationDate,
    [property: JsonPropertyName("index")] string Index,
    [property: JsonPropertyName("city")] string City)
{
    /// <summary>
    /// Gets the operation instant normalised to UTC.
    /// </summary>
    /// <remarks>
    /// The provider answers with an offset (<c>2026-10-03T16:45:00.000+03:00</c>), so the offset is applied
    /// here rather than when the value is formatted for display.
    /// </remarks>
    public DateTime OperationDateUtc => OperationDate.UtcDateTime;
}
