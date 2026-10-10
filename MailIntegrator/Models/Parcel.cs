using MailIntegrator.Infrastructure;

namespace MailIntegrator.Models;

/// <summary>
/// A tracked parcel and its synchronisation state with the Почта России and 1C ERP services.
/// </summary>
/// <remarks>
/// Every <see cref="DateTime"/> property is persisted in UTC. The Moscow time projections are
/// read-only convenience members for presentation and are never written to the database.
/// </remarks>
public sealed class Parcel
{
    /// <summary>
    /// Gets or sets the surrogate primary key. Zero until the row has been inserted.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the carrier tracking number. Required and unique (case-insensitive).
    /// </summary>
    public string TrackId { get; set; } = string.Empty;

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
    /// Gets or sets the last time the carrier status was polled, in UTC.
    /// </summary>
    public DateTime? LastCheckedDatetimeUtc { get; set; }

    /// <summary>
    /// Gets or sets the most recently observed carrier status text.
    /// </summary>
    public string? LastStatus { get; set; }

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

    /// <summary>
    /// Gets the creation instant expressed in Moscow time.
    /// </summary>
    public DateTime CreatedDatetimeMsk => MoscowTime.ToMoscow(CreatedDatetimeUtc);

    /// <summary>
    /// Gets the dispatch instant expressed in Moscow time.
    /// </summary>
    public DateTime? SentDatetimeMsk => MoscowTime.ToMoscow(SentDatetimeUtc);

    /// <summary>
    /// Gets the delivery instant expressed in Moscow time.
    /// </summary>
    public DateTime? ReceivedDatetimeMsk => MoscowTime.ToMoscow(ReceivedDatetimeUtc);

    /// <summary>
    /// Gets the last status check instant expressed in Moscow time.
    /// </summary>
    public DateTime? LastCheckedDatetimeMsk => MoscowTime.ToMoscow(LastCheckedDatetimeUtc);
}
