using MailIntegrator.Data;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// Default <see cref="IParcelService"/> implementation.
/// </summary>
public sealed class ParcelService : IParcelService
{
    private readonly IParcelRepository _repository;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelService"/> class.
    /// </summary>
    /// <param name="repository">The parcel store.</param>
    /// <param name="clock">The clock used to stamp creation instants.</param>
    public ParcelService(IParcelRepository repository, IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public IReadOnlyList<Parcel> GetAllParcels() => _repository.GetAll();

    /// <inheritdoc />
    /// <remarks>
    /// Documented defaults applied here: creation instant is "now", the 1C flag starts as
    /// <see langword="false"/>, and the sync owned fields start empty.
    /// </remarks>
    public Parcel CreateParcel(string trackId)
    {
        var normalizedTrackId = NormalizeTrackId(trackId);
        EnsureTrackIdIsUsable(normalizedTrackId, excludingId: null);

        var parcel = new Parcel
        {
            TrackId = normalizedTrackId,
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
    public Parcel UpdateEditableFields(long id, string trackId, string? comment)
    {
        var parcel = _repository.GetById(id)
            ?? throw new ParcelValidationException($"Parcel {id} does not exist.");

        EnsureNotMigrated(parcel);

        var normalizedTrackId = NormalizeTrackId(trackId);
        EnsureTrackIdIsUsable(normalizedTrackId, excludingId: id);

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
    public bool IsTrackIdAvailable(string trackId, long? excludingId = null) =>
        !string.IsNullOrWhiteSpace(trackId) && !_repository.TrackIdExists(trackId, excludingId);

    /// <summary>
    /// Trims surrounding whitespace from a tracking number.
    /// </summary>
    /// <param name="trackId">The raw tracking number.</param>
    /// <returns>The trimmed tracking number.</returns>
    private static string NormalizeTrackId(string trackId) => trackId?.Trim() ?? string.Empty;

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
    /// Validates a tracking number for presence, format and uniqueness.
    /// </summary>
    /// <param name="trackId">The already normalised tracking number.</param>
    /// <param name="excludingId">An optional parcel to ignore during the uniqueness check.</param>
    /// <exception cref="ParcelValidationException">The tracking number is invalid or already in use.</exception>
    private void EnsureTrackIdIsUsable(string trackId, long? excludingId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            throw new ParcelValidationException("A tracking number is required.");
        }

        if (_repository.TrackIdExists(trackId, excludingId))
        {
            throw new ParcelValidationException($"Tracking number '{trackId}' is already in use.");
        }
    }
}
