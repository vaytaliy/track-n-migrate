using System.IO;
using MailIntegrator.Models;

namespace MailIntegrator.Configuration;

/// <summary>
/// Resolves the status that belongs to a provider operation type, per tracking service.
/// </summary>
/// <remarks>
/// The catalog is built once from <see cref="AppConfig"/> at start-up and is the single translation point
/// between the provider vocabularies and <see cref="ParcelStatus"/>. A service without an entry, or an
/// operation type the operator did not map, becomes <see cref="ParcelStatus.Unknown"/> rather than an
/// invented value. A mapping onto a status name that is not a <see cref="ParcelStatus"/> member fails at
/// construction time, while the mistake is still easy to locate.
/// </remarks>
public sealed class StatusMappingCatalog
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, ParcelStatus>> _statusByService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusMappingCatalog"/> class.
    /// </summary>
    /// <param name="config">The configuration that carries the provider mappings.</param>
    /// <exception cref="InvalidDataException">A mapping names a status that does not exist.</exception>
    public StatusMappingCatalog(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _statusByService = BuildLookup(config);
    }

    /// <summary>
    /// Gets the catalog built from <see cref="AppConfig.CreateDefault"/>, used when no file is supplied.
    /// </summary>
    public static StatusMappingCatalog Default { get; } = new(AppConfig.CreateDefault());

    /// <summary>
    /// Maps a provider operation type onto the generic status of one tracking service.
    /// </summary>
    /// <param name="serviceCode">The tracking service code that owns the operation type.</param>
    /// <param name="operationType">The provider operation type, for example <c>Вручение</c>.</param>
    /// <returns>The configured status, or <see cref="ParcelStatus.Unknown"/> when nothing is mapped.</returns>
    public ParcelStatus MapStatus(string? serviceCode, string? operationType)
    {
        if (string.IsNullOrWhiteSpace(serviceCode) || string.IsNullOrWhiteSpace(operationType))
        {
            return ParcelStatus.Unknown;
        }

        return _statusByService.TryGetValue(serviceCode.Trim(), out var serviceMappings)
            && serviceMappings.TryGetValue(operationType.Trim(), out var status)
                ? status
                : ParcelStatus.Unknown;
    }

    /// <summary>
    /// Turns the raw configuration text into a case-insensitive lookup of parsed statuses.
    /// </summary>
    /// <param name="config">The configuration to read.</param>
    /// <returns>The lookup used by <see cref="MapStatus"/>.</returns>
    /// <exception cref="InvalidDataException">A mapping names a status that does not exist.</exception>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ParcelStatus>> BuildLookup(AppConfig config)
    {
        var lookup = new Dictionary<string, IReadOnlyDictionary<string, ParcelStatus>>(StringComparer.OrdinalIgnoreCase);

        if (config.StatusMappings is null)
        {
            return lookup;
        }

        foreach (var (serviceCode, serviceMappings) in config.StatusMappings)
        {
            if (string.IsNullOrWhiteSpace(serviceCode))
            {
                continue;
            }

            var statusByOperationType = new Dictionary<string, ParcelStatus>(StringComparer.OrdinalIgnoreCase);
            if (serviceMappings is null)
            {
                lookup[serviceCode.Trim()] = statusByOperationType;
                continue;
            }

            foreach (var (operationType, statusName) in serviceMappings)
            {
                if (string.IsNullOrWhiteSpace(operationType))
                {
                    continue;
                }

                statusByOperationType[operationType.Trim()] = ParseStatus(serviceCode, operationType, statusName);
            }

            lookup[serviceCode.Trim()] = statusByOperationType;
        }

        return lookup;
    }

    /// <summary>
    /// Parses one configured status name.
    /// </summary>
    /// <param name="serviceCode">The service the mapping belongs to, used in the error message.</param>
    /// <param name="operationType">The operation type being mapped, used in the error message.</param>
    /// <param name="statusName">The configured status name.</param>
    /// <returns>The parsed status.</returns>
    /// <exception cref="InvalidDataException">The name is not a member of <see cref="ParcelStatus"/>.</exception>
    private static ParcelStatus ParseStatus(string serviceCode, string operationType, string? statusName)
    {
        if (Enum.TryParse<ParcelStatus>(statusName, ignoreCase: true, out var status) && Enum.IsDefined(status))
        {
            return status;
        }

        throw new InvalidDataException(
            $"appConfig.json maps '{operationType}' of service '{serviceCode}' to unknown status '{statusName}'. " +
            $"Allowed values: {string.Join(", ", Enum.GetNames<ParcelStatus>())}.");
    }
}
