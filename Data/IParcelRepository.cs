using MailIntegrator.Models;

namespace MailIntegrator.Data;

/// <summary>
/// Persistence operations for <see cref="Parcel"/> records.
/// </summary>
public interface IParcelRepository
{
    /// <summary>
    /// Returns every parcel ordered by creation instant descending, which is the grid's default ordering.
    /// </summary>
    /// <returns>The stored parcels.</returns>
    IReadOnlyList<Parcel> GetAll();

    /// <summary>
    /// Finds a single parcel by primary key.
    /// </summary>
    /// <param name="id">The surrogate key.</param>
    /// <returns>The matching parcel, or <see langword="null"/> when it does not exist.</returns>
    Parcel? GetById(long id);

    /// <summary>
    /// Inserts a new parcel and assigns the generated key back to the instance.
    /// </summary>
    /// <param name="parcel">The parcel to insert.</param>
    /// <returns>The generated primary key.</returns>
    long Insert(Parcel parcel);

    /// <summary>
    /// Writes all mutable columns of an existing parcel.
    /// </summary>
    /// <param name="parcel">The parcel carrying the new values; <see cref="Parcel.Id"/> selects the row.</param>
    /// <returns><see langword="true"/> when a row was updated.</returns>
    bool Update(Parcel parcel);

    /// <summary>
    /// Permanently removes a parcel, but only while it has not been exported to 1C.
    /// </summary>
    /// <param name="id">The surrogate key.</param>
    /// <returns><see langword="true"/> when a row was deleted.</returns>
    bool Delete(long id);

    /// <summary>
    /// Finds every parcel registered under one provider and tracking number.
    /// </summary>
    /// <param name="trackingServiceCode">The provider code; compared case-insensitively.</param>
    /// <param name="trackId">The tracking number; compared case-insensitively.</param>
    /// <returns>The matching parcels, ordered by identifier.</returns>
    IReadOnlyList<Parcel> GetByTrackId(string trackingServiceCode, string trackId);

    /// <summary>
    /// Checks whether a payment number is already in use.
    /// </summary>
    /// <param name="paymentNumber">The payment number to look for; compared case-insensitively.</param>
    /// <param name="excludingId">An optional row to ignore, used when validating an existing record.</param>
    /// <returns><see langword="true"/> when another row already uses the payment number.</returns>
    bool PaymentNumberExists(string paymentNumber, long? excludingId = null);
}
