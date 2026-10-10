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
    /// Returns one stored parcel, or <see langword="null"/> when it does not exist.
    /// </summary>
    /// <param name="id">The identifier of the parcel to load.</param>
    /// <returns>The stored parcel.</returns>
    Parcel? GetParcel(long id);

    /// <summary>
    /// Creates a parcel from a user supplied payment number, tracking number and provider, applying the
    /// documented defaults.
    /// </summary>
    /// <param name="paymentNumber">The business key entered by the operator; required and unique.</param>
    /// <param name="trackId">The tracking number entered by the operator; required, may repeat.</param>
    /// <param name="trackingServiceCode">The code of a registered tracking provider.</param>
    /// <returns>The persisted parcel, including its generated identifier.</returns>
    /// <exception cref="ParcelValidationException">
    /// The payment number is missing or duplicated, the tracking number is missing, or the provider code
    /// is unknown.
    /// </exception>
    Parcel CreateParcel(string paymentNumber, string trackId, string trackingServiceCode);

    /// <summary>
    /// Applies a tracking result to a single parcel that has not reached a final status.
    /// </summary>
    /// <param name="id">The identifier of the parcel to update.</param>
    /// <param name="result">The adapted provider answer.</param>
    /// <returns>
    /// <see langword="true"/> when the parcel was updated; <see langword="false"/> when it does not exist
    /// or is already in a final state.
    /// </returns>
    bool ApplyTrackingResult(long id, TrackingResult result);

    /// <summary>
    /// Applies one poll result to every parcel registered under the same provider and tracking number, so
    /// duplicated tracking numbers cannot drift apart.
    /// </summary>
    /// <param name="trackingServiceCode">The provider that was polled.</param>
    /// <param name="trackId">The tracking number that was polled.</param>
    /// <param name="result">The adapted provider answer.</param>
    /// <returns>The number of parcels that were actually updated; final parcels are skipped.</returns>
    int ApplyTrackingResultToTrackId(string trackingServiceCode, string trackId, TrackingResult result);

    /// <summary>
    /// Updates the fields an operator is allowed to change on a parcel that has not been exported to 1C.
    /// </summary>
    /// <param name="id">The identifier of the parcel to update.</param>
    /// <param name="paymentNumber">The new payment number; required and unique.</param>
    /// <param name="trackId">The new tracking number; required, may repeat.</param>
    /// <param name="comment">The new comment, or <see langword="null"/> to clear it.</param>
    /// <returns>The updated parcel.</returns>
    /// <exception cref="ParcelValidationException">The parcel is missing, locked, or a field is invalid.</exception>
    Parcel UpdateEditableFields(long id, string paymentNumber, string trackId, string? comment);

    /// <summary>
    /// Permanently removes a parcel that has not been exported to 1C.
    /// </summary>
    /// <param name="id">The identifier of the parcel to remove.</param>
    /// <returns><see langword="true"/> when a parcel was removed.</returns>
    /// <exception cref="ParcelValidationException">The parcel exists but is locked because it was exported to 1C.</exception>
    bool DeleteParcel(long id);

    /// <summary>
    /// Reports whether a payment number can be used without colliding with an existing parcel.
    /// </summary>
    /// <param name="paymentNumber">The candidate payment number.</param>
    /// <param name="excludingId">An optional parcel to ignore, used when re-validating an existing row.</param>
    /// <returns><see langword="true"/> when the payment number is free to use.</returns>
    bool IsPaymentNumberAvailable(string paymentNumber, long? excludingId = null);
}
