namespace MailIntegrator.Models;

/// <summary>
/// A tracked parcel and its synchronisation state with a tracking provider and the 1C ERP target.
/// </summary>
/// <remarks>
/// Every <see cref="DateTime"/> property is persisted in UTC; the row view model converts to the
/// workstation's local time for display. <see cref="TrackingServiceCode"/> is written once at creation
/// and is never modified afterwards. <see cref="Id"/> is the surrogate key; <see cref="PaymentNumber"/> is
/// the business key used by the grid and by the 1C migration.
/// </remarks>
public sealed class Parcel
{
    /// <summary>
    /// Gets or sets the surrogate primary key. Zero until the row has been inserted.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the payment number: the business key of the parcel. It is required, unique
    /// (case-insensitive) and identifies the record in the 1C target system.
    /// </summary>
    public string PaymentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the carrier tracking number. Required, but deliberately not unique: several parcels
    /// may share one tracking number and are then polled and updated together.
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the code of the tracking provider that owns this parcel.
    /// <see langword="null"/> for rows created before providers existed; set on creation and read-only
    /// afterwards.
    /// </summary>
    public string? TrackingServiceCode { get; set; }

    /// <summary>
    /// Gets or sets the instant the parcel record was created, in UTC.
    /// </summary>
    public DateTime CreatedDatetimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the instant the parcel was handed to the carrier, in UTC.
    /// </summary>
    public DateTime? SentDatetimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the instant the parcel was delivered, in UTC.
    /// </summary>
    public DateTime? ReceivedDatetimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the instant of the last status report, in UTC. It is populated by a synchronisation
    /// pass from <see cref="Services.TrackingResult.StatusDatetimeUtc"/>.
    /// </summary>
    public DateTime? LastCheckedDatetimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the most recently observed generic status.
    /// </summary>
    public ParcelStatus? LastStatus { get; set; }

    /// <summary>
    /// Gets or sets the free-form operator comment.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the parcel has been exported to 1C ERP.
    /// Always <see langword="false"/> on creation and read-only afterwards.
    /// </summary>
    public bool IsMigratedTo1CFlag { get; set; }

    /// <summary>
    /// Gets or sets the instant the parcel was exported to 1C ERP, in UTC.
    /// </summary>
    public DateTime? MigratedTo1CDatetimeUtc { get; set; }
}
