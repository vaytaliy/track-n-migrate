using System.Text.Json;
using System.Text.Json.Serialization;

namespace MailIntegrator.Services;

/// <summary>
/// The payload answered by the <c>/1.0/trace</c> endpoint of Почта России.
/// </summary>
/// <param name="Barcode">The tracking number the answer belongs to.</param>
/// <param name="History">The operation history, oldest first; may be absent for an unknown tracking number.</param>
public sealed record PochtaRussiaTraceResponse(
    [property: JsonPropertyName("barcode")] string Barcode,
    [property: JsonPropertyName("history")] IReadOnlyList<PochtaRussiaTraceEntry>? History)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Deserializes a raw trace payload.
    /// </summary>
    /// <param name="json">The endpoint response body.</param>
    /// <returns>The parsed response.</returns>
    /// <exception cref="SyncException">The body is empty or is not a valid trace response.</exception>
    public static PochtaRussiaTraceResponse Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SyncException("Служба «Почта России» вернула пустой ответ.");
        }

        return JsonSerializer.Deserialize<PochtaRussiaTraceResponse>(json, SerializerOptions)
            ?? throw new SyncException("Не удалось разобрать ответ службы «Почта России».");
    }

    /// <summary>
    /// Finds the operation record with the latest <c>operation-date</c>.
    /// </summary>
    /// <returns>The latest record, or <see langword="null"/> when the history is empty.</returns>
    public PochtaRussiaTraceEntry? FindLatestEntry()
    {
        if (History is null || History.Count == 0)
        {
            return null;
        }

        return History.MaxBy(entry => entry.OperationDate);
    }
}
