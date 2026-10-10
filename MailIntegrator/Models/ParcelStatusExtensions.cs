namespace MailIntegrator.Models;

/// <summary>
/// Maps <see cref="ParcelStatus"/> values to their presentation labels and classifies final states.
/// </summary>
/// <remarks>
/// This is the only place where a status becomes text, so no magic status strings exist in the views
/// or view models.
/// </remarks>
public static class ParcelStatusExtensions
{
    /// <summary>
    /// Returns the Russian label shown for a status.
    /// </summary>
    /// <param name="status">The status to label.</param>
    /// <returns>The label used by the grid.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The status is not recognised.</exception>
    public static string ToDisplayLabel(this ParcelStatus status) => status switch
    {
        ParcelStatus.Unknown => "?",
        ParcelStatus.Processing => "В процессе",
        ParcelStatus.InTransit => "Транзит",
        ParcelStatus.Customs => "Таможня",
        ParcelStatus.OutForDelivery => "Доставляется",
        ParcelStatus.Exception => "Ошибка",
        ParcelStatus.Delivered => "Доставлено",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown parcel status."),
    };

    /// <summary>
    /// Reports whether a status ends the tracking lifecycle, so a later synchronisation pass can skip it.
    /// </summary>
    /// <param name="status">The status to classify.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="ParcelStatus.Delivered"/> and
    /// <see cref="ParcelStatus.Exception"/>.
    /// </returns>
    public static bool IsFinal(this ParcelStatus status) =>
        status is ParcelStatus.Delivered or ParcelStatus.Exception;
}
