using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;

namespace MailIntegrator.ViewModels;

/// <summary>
/// Presents a single grid row, including the always-present draft row used to add new parcels.
/// </summary>
/// <remarks>
/// Existing rows wrap a persisted <see cref="Parcel"/>; the draft row wraps an unsaved instance whose
/// creation instant tracks the clock until the operator commits it.
/// </remarks>
public sealed partial class ParcelRowViewModel : ObservableValidator
{
    /// <summary>The display format used for every timestamp in the grid.</summary>
    public const string TimestampFormat = "dd.MM.yyyy HH:mm:ss";

    /// <summary>The placeholder shown for columns that have no value.</summary>
    public const string EmptyValueDisplay = "—";

    /// <summary>The marker shown on the draft row for columns the system fills in automatically.</summary>
    public const string AutomaticValueDisplay = "Авто";

    /// <summary>The prompt shown in the comment cell of an existing parcel without a comment.</summary>
    public const string EmptyCommentPlaceholder = "Нажмите для добавления комментария...";

    /// <summary>The prompt shown in the comment cell of the draft row.</summary>
    public const string DraftCommentPlaceholder = "Доп. комментарий";

    /// <summary>The prompt shown in the tracking number cell of the draft row.</summary>
    public const string DraftTrackIdPlaceholder = "ВВЕДИТЕ ТРЕК-НОМЕР";

    /// <summary>The caption shown under the comment of a parcel that was exported to 1C.</summary>
    public const string LockedCommentCaption = "(заблокировано для редактирования)";

    private readonly Parcel _parcel;
    private readonly IClock _clock;
    private readonly Func<string, long?, bool> _isTrackIdAvailable;

    /// <summary>
    /// Backing field for <see cref="TrackId"/>.
    /// </summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Укажите трекинг-номер")]
    [CustomValidation(typeof(ParcelRowViewModel), nameof(ValidateTrackIdUniqueness))]
    private string _trackId;

    /// <summary>
    /// Backing field for <see cref="Comment"/>.
    /// </summary>
    [ObservableProperty]
    private string _comment;

    /// <summary>
    /// Backing field for <see cref="RowNumber"/>.
    /// </summary>
    [ObservableProperty]
    private int _rowNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelRowViewModel"/> class.
    /// </summary>
    /// <param name="parcel">The parcel backing the row.</param>
    /// <param name="isDraft">Whether the row is the unsaved draft row.</param>
    /// <param name="clock">The clock used to display the draft's automatic creation instant.</param>
    /// <param name="isTrackIdAvailable">Callback that reports whether a tracking number is still free.</param>
    private ParcelRowViewModel(
        Parcel parcel,
        bool isDraft,
        IClock clock,
        Func<string, long?, bool> isTrackIdAvailable)
    {
        _parcel = parcel;
        _clock = clock;
        _isTrackIdAvailable = isTrackIdAvailable;
        IsDraft = isDraft;

        // Assign the backing fields directly so that constructing a row does not run validation.
        _trackId = parcel.TrackId;
        _comment = parcel.Comment ?? string.Empty;
    }

    /// <summary>
    /// Creates a row for an already persisted parcel.
    /// </summary>
    /// <param name="parcel">The persisted parcel.</param>
    /// <param name="clock">The clock used for display only.</param>
    /// <param name="isTrackIdAvailable">Callback that reports whether a tracking number is still free.</param>
    /// <returns>The row view model.</returns>
    public static ParcelRowViewModel ForExisting(
        Parcel parcel,
        IClock clock,
        Func<string, long?, bool> isTrackIdAvailable) =>
        new(parcel, isDraft: false, clock, isTrackIdAvailable);

    /// <summary>
    /// Creates the unsaved draft row shown at the bottom of the grid.
    /// </summary>
    /// <param name="clock">The clock that supplies the automatic creation instant.</param>
    /// <param name="isTrackIdAvailable">Callback that reports whether a tracking number is still free.</param>
    /// <returns>The draft row view model.</returns>
    public static ParcelRowViewModel CreateDraft(
        IClock clock,
        Func<string, long?, bool> isTrackIdAvailable) =>
        new(
            new Parcel
            {
                TrackId = string.Empty,
                CreatedDatetimeUtc = clock.UtcNow,
                IsMigratedTo1CFlag = false,
            },
            isDraft: true,
            clock,
            isTrackIdAvailable);

    /// <summary>
    /// Gets a value indicating whether this row is the unsaved draft row.
    /// </summary>
    public bool IsDraft { get; }

    /// <summary>
    /// Gets the identifier of the backing parcel; zero for the draft row.
    /// </summary>
    public long Id => _parcel.Id;

    // The raw UTC values below exist so grid columns can sort chronologically rather than by the
    // formatted display text.

    /// <summary>
    /// Gets the raw creation instant, used for chronological sorting.
    /// </summary>
    public DateTime CreatedDatetimeUtc => _parcel.CreatedDatetimeUtc;

    /// <summary>
    /// Gets the raw dispatch instant, used for chronological sorting.
    /// </summary>
    public DateTime? SentDatetimeUtc => _parcel.SentDatetimeUtc;

    /// <summary>
    /// Gets the raw delivery instant, used for chronological sorting.
    /// </summary>
    public DateTime? ReceivedDatetimeUtc => _parcel.ReceivedDatetimeUtc;

    /// <summary>
    /// Gets the raw last-check instant, used for chronological sorting.
    /// </summary>
    public DateTime? LastCheckedDatetimeUtc => _parcel.LastCheckedDatetimeUtc;

    /// <summary>
    /// Gets the raw 1C export instant, used for chronological sorting.
    /// </summary>
    public DateTime? MigratedTo1CDatetimeUtc => _parcel.MigratedTo1CDatetimeUtc;

    /// <summary>
    /// Gets the raw carrier status text, used for sorting.
    /// </summary>
    public string? LastStatus => _parcel.LastStatus;

    /// <summary>
    /// Gets a value indicating whether the parcel was exported to 1C and is therefore read-only.
    /// </summary>
    public bool IsMigratedTo1CFlag => _parcel.IsMigratedTo1CFlag;

    /// <summary>
    /// Gets a value indicating whether the operator may change the editable columns of this row.
    /// The draft row is editable, saved rows are editable until they are exported to 1C.
    /// </summary>
    public bool IsEditable => !IsMigratedTo1CFlag;

    /// <summary>
    /// Gets a value indicating whether the draft row still needs its tracking number filled in.
    /// </summary>
    public bool IsTrackIdPlaceholder => string.IsNullOrWhiteSpace(TrackId);

    /// <summary>
    /// Gets a value indicating whether a tracking number has been entered on this row.
    /// </summary>
    public bool HasTrackId => !string.IsNullOrWhiteSpace(TrackId);

    /// <summary>
    /// Validates every editable column and reports whether the row can be persisted.
    /// </summary>
    /// <returns><see langword="true"/> when no validation error remains.</returns>
    public bool ValidateForSave()
    {
        ValidateAllProperties();
        return !HasErrors;
    }

    /// <summary>
    /// Gets a value indicating whether the comment column should render its prompt instead of a value.
    /// </summary>
    public bool IsCommentPlaceholder => string.IsNullOrWhiteSpace(Comment);

    /// <summary>
    /// Gets a value indicating whether the comment lock caption should be shown.
    /// </summary>
    public bool ShowLockedCommentCaption => IsMigratedTo1CFlag;

    /// <summary>
    /// Gets the row number shown in the leading column, or an asterisk for the draft row.
    /// </summary>
    public string RowNumberDisplay =>
        IsDraft ? "*" : RowNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the tracking number cell text, falling back to the appropriate prompt.
    /// </summary>
    public string TrackIdDisplay =>
        IsTrackIdPlaceholder
            ? (IsDraft ? DraftTrackIdPlaceholder : EmptyValueDisplay)
            : TrackId;

    /// <summary>
    /// Gets the creation instant formatted for display. Moscow time for saved rows, live clock for the draft.
    /// </summary>
    public string CreatedDatetimeDisplay
    {
        get
        {
            if (IsDraft)
            {
                return $"{Format(_clock.UtcNow)} ({AutomaticValueDisplay})";
            }

            return Format(_parcel.CreatedDatetimeUtc);
        }
    }

    /// <summary>
    /// Gets the dispatch instant formatted for display in Moscow time, or the automatic marker on the
    /// draft row because the value is only known after the carrier is contacted.
    /// </summary>
    public string SentDatetimeDisplay =>
        IsDraft ? AutomaticValueDisplay : FormatOptional(_parcel.SentDatetimeUtc);

    /// <summary>
    /// Gets the delivery instant formatted for display in Moscow time, or the automatic marker on the
    /// draft row.
    /// </summary>
    public string ReceivedDatetimeDisplay =>
        IsDraft ? AutomaticValueDisplay : FormatOptional(_parcel.ReceivedDatetimeUtc);

    /// <summary>
    /// Gets the last status check instant formatted for display in Moscow time.
    /// </summary>
    /// <remarks>
    /// Stored in UTC, displayed in Moscow time like the other business timestamps. The approved mockup
    /// shows no suffix for this column.
    /// </remarks>
    public string LastCheckedDatetimeDisplay =>
        IsDraft ? AutomaticValueDisplay : FormatOptional(_parcel.LastCheckedDatetimeUtc);

    /// <summary>
    /// Gets the 1C export instant formatted for display in UTC.
    /// </summary>
    public string MigratedTo1CDatetimeDisplay =>
        IsDraft ? AutomaticValueDisplay : FormatOptionalUtc(_parcel.MigratedTo1CDatetimeUtc);

    /// <summary>
    /// Gets the carrier status text, or the empty-value placeholder when no status was observed yet.
    /// The draft row shows the automatic marker instead, because the status is filled in by a sync run.
    /// </summary>
    public string LastStatusDisplay => IsDraft
        ? AutomaticValueDisplay
        : (string.IsNullOrWhiteSpace(_parcel.LastStatus) ? EmptyValueDisplay : _parcel.LastStatus!);

    /// <summary>
    /// Gets a value indicating whether a carrier status has been observed.
    /// </summary>
    public bool HasLastStatus => !string.IsNullOrWhiteSpace(_parcel.LastStatus);

    /// <summary>
    /// Gets the comment cell text, falling back to the appropriate prompt.
    /// </summary>
    public string CommentDisplay =>
        IsCommentPlaceholder
            ? (IsDraft
                ? DraftCommentPlaceholder
                : (IsMigratedTo1CFlag ? EmptyValueDisplay : EmptyCommentPlaceholder))
            : Comment;

    /// <summary>
    /// Validates that a tracking number is not already used by another parcel.
    /// </summary>
    /// <param name="value">The candidate tracking number.</param>
    /// <param name="context">The validation context, used to reach the owning row.</param>
    /// <returns>The validation outcome.</returns>
    public static ValidationResult? ValidateTrackIdUniqueness(object? value, ValidationContext context)
    {
        if (context.ObjectInstance is not ParcelRowViewModel row)
        {
            return ValidationResult.Success;
        }

        var trackId = value as string;
        if (string.IsNullOrWhiteSpace(trackId))
        {
            // The Required attribute owns the "missing value" case.
            return ValidationResult.Success;
        }

        long? excludingId = row.IsDraft ? null : row.Id;
        return row._isTrackIdAvailable(trackId, excludingId)
            ? ValidationResult.Success
            : new ValidationResult("Такой трекинг-номер уже добавлен");
    }

    /// <summary>
    /// Re-reads the clock so the draft row's automatic creation instant stays current.
    /// </summary>
    public void RefreshDraftTimestamp()
    {
        if (IsDraft)
        {
            OnPropertyChanged(nameof(CreatedDatetimeDisplay));
        }
    }

    /// <summary>
    /// Copies the edited values back onto the backing parcel after a successful save.
    /// </summary>
    /// <param name="trackId">The persisted tracking number.</param>
    /// <param name="comment">The persisted comment, or <see langword="null"/>.</param>
    public void ApplyPersistedValues(string trackId, string? comment)
    {
        TrackId = trackId;
        Comment = comment ?? string.Empty;
        _parcel.TrackId = trackId;
        _parcel.Comment = comment;
    }

    /// <summary>
    /// Gets the backing parcel so the owning view model can hand it to the service layer.
    /// </summary>
    /// <returns>The backing parcel.</returns>
    public Parcel GetParcel() => _parcel;

    /// <summary>
    /// Formats a mandatory timestamp as Moscow time.
    /// </summary>
    /// <param name="utc">The instant to format.</param>
    /// <returns>The formatted text.</returns>
    private static string Format(DateTime utc) =>
        MoscowTime.ToMoscow(utc).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats an optional Moscow timestamp, or the empty-value placeholder.
    /// </summary>
    /// <param name="utc">The instant to format.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatOptional(DateTime? utc) =>
        utc is null ? EmptyValueDisplay : Format(utc.Value);

    /// <summary>
    /// Formats an optional UTC timestamp with an explicit UTC marker, or the empty-value placeholder.
    /// </summary>
    /// <param name="utc">The instant to format.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatOptionalUtc(DateTime? utc) =>
        utc is null
            ? EmptyValueDisplay
            : $"{utc.Value.ToString(TimestampFormat, CultureInfo.InvariantCulture)} UTC";

    /// <summary>
    /// Notifies the computed display members when the tracking number changes.
    /// </summary>
    /// <param name="value">The new tracking number.</param>
    partial void OnTrackIdChanged(string value)
    {
        OnPropertyChanged(nameof(TrackIdDisplay));
        OnPropertyChanged(nameof(IsTrackIdPlaceholder));
    }

    /// <summary>
    /// Notifies the computed display members when the comment changes.
    /// </summary>
    /// <param name="value">The new comment.</param>
    partial void OnCommentChanged(string value)
    {
        OnPropertyChanged(nameof(CommentDisplay));
        OnPropertyChanged(nameof(IsCommentPlaceholder));
    }

    /// <summary>
    /// Notifies the computed display member when the row number changes.
    /// </summary>
    /// <param name="value">The new row number.</param>
    partial void OnRowNumberChanged(int value) => OnPropertyChanged(nameof(RowNumberDisplay));
}
