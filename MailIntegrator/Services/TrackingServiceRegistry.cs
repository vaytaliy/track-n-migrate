namespace MailIntegrator.Services;

/// <summary>
/// Default <see cref="ITrackingServiceRegistry"/> implementation over an in-memory provider list.
/// </summary>
/// <remarks>
/// Providers are registered once in the composition root. Misconfiguration (a blank or duplicated code)
/// fails fast in the constructor rather than surfacing while routing a parcel.
/// </remarks>
public sealed class TrackingServiceRegistry : ITrackingServiceRegistry
{
    private readonly IReadOnlyList<ITrackingService> _services;
    private readonly Dictionary<string, ITrackingService> _servicesByCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrackingServiceRegistry"/> class.
    /// </summary>
    /// <param name="services">The providers to register, in presentation order.</param>
    /// <exception cref="ArgumentException">
    /// A provider has a blank code, or two providers share a code (compared case-insensitively).
    /// </exception>
    public TrackingServiceRegistry(IEnumerable<ITrackingService> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services.ToList();
        _servicesByCode = new Dictionary<string, ITrackingService>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in _services)
        {
            ArgumentNullException.ThrowIfNull(service);

            var code = service.Descriptor.Code;
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("A tracking provider must declare a non-blank code.", nameof(services));
            }

            if (!_servicesByCode.TryAdd(code, service))
            {
                throw new ArgumentException($"Duplicate tracking provider code '{code}'.", nameof(services));
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ITrackingService> Services => _services;

    /// <inheritdoc />
    public ITrackingService? FindByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return _servicesByCode.TryGetValue(code.Trim(), out var service) ? service : null;
    }

    /// <inheritdoc />
    public bool ContainsCode(string? code) => FindByCode(code) is not null;
}
