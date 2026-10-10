using System.Text.Json.Serialization;

namespace MailIntegrator.Configuration;

/// <summary>
/// The user editable application configuration loaded from <c>appConfig.json</c>.
/// </summary>
/// <remarks>
/// The file is deployed next to the executable and is re-read on every start, so an operator can change a
/// mapping without touching the code or the database. Every provider answers with its own operation type
/// vocabulary; this configuration is the only place where that vocabulary is translated.
/// </remarks>
public sealed class AppConfig
{
    /// <summary>
    /// Gets or sets the provider mapping, keyed by tracking service code and then by provider operation
    /// type, whose value is the name of a <see cref="Models.ParcelStatus"/> member.
    /// </summary>
    [JsonPropertyName("statusMappings")]
    public Dictionary<string, Dictionary<string, string>> StatusMappings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates the configuration that ships with the application.
    /// </summary>
    /// <returns>The default configuration, populated with the known provider mappings.</returns>
    public static AppConfig CreateDefault() => new()
    {
        StatusMappings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["PochtaRussia"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Прием"] = nameof(Models.ParcelStatus.Processing),
                ["Передано курьеру"] = nameof(Models.ParcelStatus.OutForDelivery),
                ["Неудачная попытка вручения"] = nameof(Models.ParcelStatus.Exception),
                ["Вручение"] = nameof(Models.ParcelStatus.Delivered),
                ["Возврат"] = nameof(Models.ParcelStatus.Delivered),
                ["Вручение отправителю"] = nameof(Models.ParcelStatus.Delivered),
            },
            ["DHL"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ["DPD"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        },
    };
}
