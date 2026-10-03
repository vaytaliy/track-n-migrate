using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// Applies the parcel business rules on top of persistence.
/// </summary>
public interface IParcelService
{
    /// <summary>
    /// Returns every parcel in the grid's default order (creation instant descending).
    /// </summary>
    /// <returns>The stored parcels.</returns>
    IReadOnlyList<Parcel> GetAllParcels();

    /// <summary>
    /// Creates a parcel from a user supplied tracking number and provider, applying the documented defaults.
    /// </summary>
    /// <param name="trackId">The tracking number entered by the operator.</param>
    /// <param name="trackingServiceCode">The code of a registered tracking provider.</param>
    /// <returns>The persisted parcel, including its generated identifier.</returns>
    /// <exception cref="ParcelValidationException">
    /// The tracking number is missing, malformed or duplicated, or the provider code is unknown.
    /// </exception>
    Parcel CreateParcel(string trackId, string trackingServiceCode);

    /// <summary>
    /// Applies a tracking result to a parcel that has not reached a final status.
    /// </summary>
    /// <param name="id">The identifier of the parcel to update.</param>
    /// <param name="result">The adapted provider answer.</param>
    /// <returns>
    /// <see langword="true"/> when the parcel was updated; <see langword="false"/> when it does not exist
    /// or is already in a final state.
    /// </returns>
    bool ApplyTrackingResult(long id, TrackingResult result);

    /// <summary>
    /// Updates the fields an operator is allowed to change on a parcel that has not been exported to 1C.
    /// </summary>
    /// <param name="id">The identifier of the parcel to update.</param>
    /// <param name="trackId">The new tracking number.</param>
    /// <param name="comment">The new comment, or <see langword="null"/> to clear it.</param>
    /// <returns>The updated parcel.</returns>
    /// <exception cref="ParcelValidationException">The parcel is missing, locked, or the tracking number is invalid.</exception>
    Parcel UpdateEditableFields(long id, string trackId, string? comment);

    /// <summary>
    /// Permanently removes a parcel that has not been exported to 1C.
    /// </summary>
    /// <param name="id">The identifier of the parcel to remove.</param>
    /// <returns><see langword="true"/> when a parcel was removed.</returns>
    /// <exception cref="ParcelValidationException">The parcel exists but is locked because it was exported to 1C.</exception>
    bool DeleteParcel(long id);

    /// <summary>
    /// Reports whether a tracking number can be used without colliding with an existing parcel.
    /// </summary>
    /// <param name="trackId">The candidate tracking number.</param>
    /// <param name="excludingId">An optional parcel to ignore, used when re-validating an existing row.</param>
    /// <returns><see langword="true"/> when the tracking number is free to use.</returns>
    bool IsTrackIdAvailable(string trackId, long? excludingId = null);
}
