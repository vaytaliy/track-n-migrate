using MailIntegrator.Data;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;
using MailIntegrator.Utils;

namespace MailIntegrator.Services;

/// <summary>
/// Default <see cref="IParcelService"/> implementation.
/// </summary>
public sealed class ParcelService : IParcelService
{
    private readonly IParcelRepository _repository;
    private readonly IClock _clock;
    private readonly ITrackingServiceRegistry _trackingServiceRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelService"/> class.
    /// </summary>
    /// <param name="repository">The parcel store.</param>
    /// <param name="clock">The clock used to stamp creation instants.</param>
    /// <param name="trackingServiceRegistry">Validates the provider chosen for a new parcel.</param>
    public ParcelService(
        IParcelRepository repository,
        IClock clock,
        ITrackingServiceRegistry trackingServiceRegistry)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _trackingServiceRegistry =
            trackingServiceRegistry ?? throw new ArgumentNullException(nameof(trackingServiceRegistry));
    }

    /// <inheritdoc />
    public IReadOnlyList<Parcel> GetAllParcels() => _repository.GetAll();

    /// <inheritdoc />
    public Parcel? GetParcel(long id) => _repository.GetById(id);

    /// <inheritdoc />
    /// <remarks>
    /// Documented defaults applied here: creation instant is "now", the 1C flag starts as
    /// <see langword="false"/>, and the sync owned fields start empty. The provider is written once and
    /// is never modified afterwards.
    /// </remarks>
    public Parcel CreateParcel(string paymentNumber, string trackId, string trackingServiceCode)
    {
        var normalizedPaymentNumber = TextField.Normalize(paymentNumber);
        EnsurePaymentNumberIsUsable(normalizedPaymentNumber, excludingId: null);
        var normalizedTrackId = TextField.Normalize(trackId);
        EnsureTrackIdIsPresent(normalizedTrackId);
        var normalizedProviderCode = NormalizeProviderCode(trackingServiceCode);

        var parcel = new Parcel
        {
            PaymentNumber = normalizedPaymentNumber,
            TrackId = normalizedTrackId,
            TrackingServiceCode = normalizedProviderCode,
            CreatedDatetimeUtc = _clock.UtcNow,
            SentDatetimeUtc = null,
            ReceivedDatetimeUtc = null,
            LastCheckedDatetimeUtc = null,
            LastStatus = null,
            Comment = null,
            IsMigratedTo1CFlag = false,
            MigratedTo1CDatetimeUtc = null,
        };

        _repository.Insert(parcel);
        return parcel;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A parcel that already reached <see cref="ParcelStatus.Delivered"/> or
    /// <see cref="ParcelStatus.Exception"/> is left untouched, which makes a synchronisation pass
    /// idempotent for finished parcels.
    /// </remarks>
    public bool ApplyTrackingResult(long id, TrackingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var parcel = _repository.GetById(id);
        if (parcel is null || parcel.LastStatus?.IsFinal() == true)
        {
            return false;
        }

        parcel.LastStatus = result.CurrentStatus;
        parcel.LastCheckedDatetimeUtc = result.StatusDatetimeUtc;

        return _repository.Update(parcel);
    }

    /// <inheritdoc />
    public int ApplyTrackingResultToTrackId(
        string trackingServiceCode,
        string trackId,
        TrackingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var updated = 0;
        foreach (var parcel in _repository.GetByTrackId(trackingServiceCode, trackId))
        {
            if (parcel.LastStatus?.IsFinal() == true)
            {
                continue;
            }

            parcel.LastStatus = result.CurrentStatus;
            parcel.LastCheckedDatetimeUtc = result.StatusDatetimeUtc;

            if (_repository.Update(parcel))
            {
                updated++;
            }
        }

        return updated;
    }

    /// <inheritdoc />
    public Parcel UpdateEditableFields(long id, string paymentNumber, string trackId, string? comment)
    {
        var parcel = _repository.GetById(id)
            ?? throw new ParcelValidationException($"Parcel {id} does not exist.");

        EnsureNotMigrated(parcel);

        var normalizedPaymentNumber = TextField.Normalize(paymentNumber);
        EnsurePaymentNumberIsUsable(normalizedPaymentNumber, excludingId: id);
        var normalizedTrackId = TextField.Normalize(trackId);
        EnsureTrackIdIsPresent(normalizedTrackId);

        parcel.PaymentNumber = normalizedPaymentNumber;
        parcel.TrackId = normalizedTrackId;
        parcel.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        _repository.Update(parcel);
        return parcel;
    }

    /// <inheritdoc />
    public bool DeleteParcel(long id)
    {
        var parcel = _repository.GetById(id);
        if (parcel is null)
        {
            return false;
        }

        EnsureNotMigrated(parcel);
        return _repository.Delete(id);
    }

    /// <inheritdoc />
    public bool IsPaymentNumberAvailable(string paymentNumber, long? excludingId = null) =>
        !TextField.IsEmpty(paymentNumber) && !_repository.PaymentNumberExists(paymentNumber, excludingId);

    /// <summary>
    /// Enforces that a parcel has not been exported to 1C, which makes it read-only.
    /// </summary>
    /// <param name="parcel">The parcel to inspect.</param>
    /// <exception cref="ParcelValidationException">The parcel is locked.</exception>
    private static void EnsureNotMigrated(Parcel parcel)
    {
        if (parcel.IsMigratedTo1CFlag)
        {
            throw new ParcelValidationException(
                $"Parcel {parcel.Id} was exported to 1C and can no longer be modified.");
        }
    }

    /// <summary>
    /// Validates a payment number for presence and uniqueness.
    /// </summary>
    /// <param name="paymentNumber">The already normalised payment number.</param>
    /// <param name="excludingId">An optional parcel to ignore during the uniqueness check.</param>
    /// <exception cref="ParcelValidationException">The payment number is missing or already in use.</exception>
    private void EnsurePaymentNumberIsUsable(string paymentNumber, long? excludingId)
    {
        if (TextField.IsEmpty(paymentNumber))
        {
            throw new ParcelValidationException("A payment number is required.");
        }

        if (_repository.PaymentNumberExists(paymentNumber, excludingId))
        {
            throw new ParcelValidationException($"Payment number '{paymentNumber}' is already in use.");
        }
    }

    /// <summary>
    /// Validates that a tracking number is present. It is deliberately not checked for uniqueness, because
    /// several parcels may be registered under the same tracking number.
    /// </summary>
    /// <param name="trackId">The already normalised tracking number.</param>
    /// <exception cref="ParcelValidationException">The tracking number is missing.</exception>
    private static void EnsureTrackIdIsPresent(string trackId)
    {
        if (TextField.IsEmpty(trackId))
        {
            throw new ParcelValidationException("A tracking number is required.");
        }
    }

    /// <summary>
    /// Validates and normalises the provider code chosen for a new parcel.
    /// </summary>
    /// <param name="trackingServiceCode">The provider code entered on the draft row.</param>
    /// <returns>The trimmed provider code.</returns>
    /// <exception cref="ParcelValidationException">The provider is missing or not registered.</exception>
    private string NormalizeProviderCode(string trackingServiceCode)
    {
        var code = trackingServiceCode?.Trim();
        if (string.IsNullOrWhiteSpace(code) || !_trackingServiceRegistry.ContainsCode(code))
        {
            throw new ParcelValidationException($"Tracking provider '{trackingServiceCode}' is not registered.");
        }

        return code;
    }
}
