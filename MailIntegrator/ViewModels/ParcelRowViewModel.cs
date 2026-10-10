using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIntegrator.Infrastructure;
using MailIntegrator.Models;
using MailIntegrator.Services;
using MailIntegrator.Utils;

namespace MailIntegrator.ViewModels;

/// <summary>
/// Presents a single grid row, including the always-present draft row used to add new parcels.
/// </summary>
/// <remarks>
/// Existing rows wrap a persisted <see cref="Parcel"/>; the draft row wraps an unsaved instance whose
/// creation instant tracks the clock until the operator commits it. Timestamps are stored in UTC and
/// converted through <see cref="ILocalTimeZone"/> for display.
/// </remarks>
public sealed partial class ParcelRowViewModel : ObservableValidator
{
    /// <summary>The display format used for every timestamp in the grid.</summary>
    public const string TimestampFormat = "dd.MM.yyyy HH:mm:ss";

    /// <summary>The placeholder shown for columns that have no value.</summary>
    public const string EmptyValueDisplay = "—";

    /// <summary>The prompt shown in the comment cell of the draft row.</summary>
    public const string DraftCommentPlaceholder = "Доп. комментарий";

    /// <summary>The prompt shown in the payment number cell of the draft row.</summary>
    public const string DraftPaymentNumberPlaceholder = "Введите номер счёта";

    /// <summary>The prompt shown in the tracking number cell of the draft row.</summary>
    public const string DraftTrackIdPlaceholder = "Введите трек-номер";

    /// <summary>The prompt shown in the provider cell of the draft row.</summary>
    public const string DraftTrackingServicePlaceholder = "Выберите службу";

    /// <summary>The caption shown under the comment of a parcel that was exported to 1C.</summary>
    public const string LockedCommentCaption = "(заблокировано для редактирования)";

    private readonly Parcel _parcel;
    private readonly IClock _clock;
    private readonly ILocalTimeZone _localTimeZone;
    private readonly Func<string, long?, bool> _isPaymentNumberAvailable;

    /// <summary>
    /// Resolves the public tracking page of a provider code and tracking number, or reports that the provider
    /// has none.
    /// </summary>
    private readonly Func<string, string, Uri?> _resolveTrackingUrl;

    /// <summary>
    /// Backing field for <see cref="PaymentNumber"/>. Required and unique: the business key of the row.
    /// </summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Укажите номер счёта")]
    [CustomValidation(typeof(ParcelRowViewModel), nameof(ValidatePaymentNumberUniqueness))]
    private string _paymentNumber;

    /// <summary>
    /// Backing field for <see cref="TrackId"/>. Required, but deliberately not unique: several rows may
    /// share one tracking number and are then polled and updated together.
    /// </summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Укажите трекинг-номер")]
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
    /// Backing field for <see cref="SelectedTrackingServiceCode"/>. Set on the draft row and read-only
    /// on saved rows.
    /// </summary>
    [ObservableProperty]
    [Required(ErrorMessage = "Укажите службу доставки")]
    private string _selectedTrackingServiceCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelRowViewModel"/> class.
    /// </summary>
    /// <param name="parcel">The parcel backing the row.</param>
    /// <param name="isDraft">Whether the row is the unsaved draft row.</param>
    /// <param name="clock">The clock used to display the draft's automatic creation instant.</param>
    /// <param name="localTimeZone">The zone used to render UTC instants.</param>
    /// <param name="trackingServices">The provider descriptors offered by the registry.</param>
    /// <param name="isPaymentNumberAvailable">Callback that reports whether a payment number is still free.</param>
    /// <param name="resolveTrackingUrl">Callback that builds the provider's public page for a tracking number,
    /// or returns <see langword="null"/> when the provider has none.</param>
    private ParcelRowViewModel(
        Parcel parcel,
        bool isDraft,
        IClock clock,
        ILocalTimeZone localTimeZone,
        IReadOnlyList<TrackingServiceDescriptor> trackingServices,
        Func<string, long?, bool> isPaymentNumberAvailable,
        Func<string, string, Uri?> resolveTrackingUrl)
    {
        _parcel = parcel;
        _clock = clock;
        _localTimeZone = localTimeZone;
        _isPaymentNumberAvailable = isPaymentNumberAvailable;
        _resolveTrackingUrl = resolveTrackingUrl;
        TrackingServices = trackingServices;
        IsDraft = isDraft;

        // Assign the backing fields directly so that constructing a row does not run validation.
        _trackId = parcel.TrackId;
        _paymentNumber = parcel.PaymentNumber;
        _comment = parcel.Comment ?? string.Empty;
        _selectedTrackingServiceCode = parcel.TrackingServiceCode ?? string.Empty;
    }

    /// <summary>
    /// Creates a row for an already persisted parcel.
    /// </summary>
    /// <param name="parcel">The persisted parcel.</param>
    /// <param name="clock">The clock used for display only.</param>
    /// <param name="localTimeZone">The zone used to render UTC instants.</param>
    /// <param name="trackingServices">The provider descriptors offered by the registry.</param>
    /// <param name="isPaymentNumberAvailable">Callback that reports whether a payment number is still free.</param>
    /// <param name="resolveTrackingUrl">Callback that builds the provider's public page for a tracking number.</param>
    /// <returns>The row view model.</returns>
    public static ParcelRowViewModel ForExisting(
        Parcel parcel,
        IClock clock,
        ILocalTimeZone localTimeZone,
        IReadOnlyList<TrackingServiceDescriptor> trackingServices,
        Func<string, long?, bool> isPaymentNumberAvailable,
        Func<string, string, Uri?> resolveTrackingUrl) =>
        new(parcel, isDraft: false, clock, localTimeZone, trackingServices, isPaymentNumberAvailable, resolveTrackingUrl);

    /// <summary>
    /// Creates the unsaved draft row shown at the bottom of the grid.
    /// </summary>
    /// <param name="clock">The clock that supplies the automatic creation instant.</param>
    /// <param name="localTimeZone">The zone used to render UTC instants.</param>
    /// <param name="trackingServices">The provider descriptors offered by the registry.</param>
    /// <param name="isPaymentNumberAvailable">Callback that reports whether a tracking number is still free.</param>
    /// <param name="resolveTrackingUrl">Callback that builds the provider's public page for a tracking number.</param>
    /// <returns>The draft row view model.</returns>
    public static ParcelRowViewModel CreateDraft(
        IClock clock,
        ILocalTimeZone localTimeZone,
        IReadOnlyList<TrackingServiceDescriptor> trackingServices,
        Func<string, long?, bool> isPaymentNumberAvailable,
        Func<string, string, Uri?> resolveTrackingUrl) =>
        new(
            new Parcel
            {
                TrackId = string.Empty,
                TrackingServiceCode = null,
                CreatedDatetimeUtc = clock.UtcNow,
                IsMigratedTo1CFlag = false,
            },
            isDraft: true,
            clock,
            localTimeZone,
            trackingServices,
            isPaymentNumberAvailable,
            resolveTrackingUrl);

    /// <summary>
    /// Gets a value indicating whether this row is the unsaved draft row.
    /// </summary>
    public bool IsDraft { get; }

    /// <summary>
    /// Gets the identifier of the backing parcel; zero for the draft row.
    /// </summary>
    public long Id => _parcel.Id;

    /// <summary>
    /// Gets the providers the operator can choose from on the draft row.
    /// </summary>
    public IReadOnlyList<TrackingServiceDescriptor> TrackingServices { get; }

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
    /// Gets the raw last status report instant, used for chronological sorting.
    /// </summary>
    public DateTime? LastCheckedDatetimeUtc => _parcel.LastCheckedDatetimeUtc;

    /// <summary>
    /// Gets the raw 1C export instant, used for chronological sorting.
    /// </summary>
    public DateTime? MigratedTo1CDatetimeUtc => _parcel.MigratedTo1CDatetimeUtc;

    /// <summary>
    /// Gets the persisted provider code, used for sorting and display.
    /// </summary>
    public string? TrackingServiceCode => _parcel.TrackingServiceCode;

    /// <summary>
    /// Gets the generic status, used for sorting and for the badge triggers in the view.
    /// </summary>
    public ParcelStatus? Status => _parcel.LastStatus;

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
    /// Gets the payment number cell: the value, or the draft prompt / empty placeholder when missing.
    /// </summary>
    public CellText PaymentNumberCell =>
        CellText.Resolve(PaymentNumber, IsDraft ? DraftPaymentNumberPlaceholder : EmptyValueDisplay);

    /// <summary>
    /// Gets the tracking number cell: the value, or the draft prompt / empty placeholder when missing.
    /// </summary>
    public CellText TrackIdCell =>
        CellText.Resolve(TrackId, IsDraft ? DraftTrackIdPlaceholder : EmptyValueDisplay);

    /// <summary>
    /// Gets a value indicating whether the provider cell has no value and should render its prompt.
    /// </summary>
    public bool IsTrackingServiceMissing =>
        string.IsNullOrWhiteSpace(IsDraft ? SelectedTrackingServiceCode : _parcel.TrackingServiceCode);

    /// <summary>
    /// Gets the comment cell: the value, the draft prompt, or the empty placeholder.
    /// </summary>
    public CellText CommentCell =>
        CellText.Resolve(Comment, IsDraft ? DraftCommentPlaceholder : EmptyValueDisplay);

    /// <summary>
    /// Gets the caption rendered under a comment that can no longer be edited, or an empty string while
    /// the comment is editable.
    /// </summary>
    public string CommentLockCaption => IsMigratedTo1CFlag ? LockedCommentCaption : string.Empty;

    /// <summary>
    /// Gets a value indicating whether a generic status has been observed.
    /// </summary>
    public bool HasLastStatus => _parcel.LastStatus is not null;

    /// <summary>
    /// Gets a value indicating whether the operator can request an individual status check for this row.
    /// The draft row has nothing to poll, a row without a provider cannot be routed, and a final status
    /// would not change any more, so all three are hidden.
    /// </summary>
    public bool CanCheckStatus =>
        !IsDraft
        && !string.IsNullOrWhiteSpace(_parcel.TrackingServiceCode)
        && _parcel.LastStatus?.IsFinal() != true;

    /// <summary>
    /// Gets the public tracking page of this parcel, or <see langword="null"/> when there is nothing to open:
    /// the draft row, a missing tracking number or provider, or a provider that publishes no page.
    /// </summary>
    public Uri? TrackingUrl
    {
        get
        {
            if (IsDraft
                || TextField.IsEmpty(TrackId)
                || TextField.IsEmpty(_parcel.TrackingServiceCode))
            {
                return null;
            }

            return _resolveTrackingUrl(_parcel.TrackingServiceCode!, TrackId);
        }
    }

    /// <summary>
    /// Gets the row number shown in the leading column, or the empty placeholder for the draft row.
    /// </summary>
    public string RowNumberDisplay =>
        IsDraft ? EmptyValueDisplay : RowNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the provider cell text: the selected display name on the draft row, the persisted provider
    /// name on saved rows, or the appropriate placeholder.
    /// </summary>
    public string TrackingServiceDisplay
    {
        get
        {
            if (IsDraft)
            {
                return IsTrackingServiceMissing
                    ? DraftTrackingServicePlaceholder
                    : ResolveTrackingServiceName(SelectedTrackingServiceCode);
            }

            return IsTrackingServiceMissing
                ? EmptyValueDisplay
                : ResolveTrackingServiceName(_parcel.TrackingServiceCode!);
        }
    }

    /// <summary>
    /// Gets the creation instant formatted for display: live local time on the draft row, the persisted
    /// instant on saved rows.
    /// </summary>
    public string CreatedDatetimeDisplay =>
        FormatLocal(IsDraft ? _clock.UtcNow : _parcel.CreatedDatetimeUtc);

    /// <summary>
    /// Gets the dispatch instant in local time, or the empty placeholder while the carrier has not reported it.
    /// </summary>
    public string SentDatetimeDisplay => FormatOptionalLocal(_parcel.SentDatetimeUtc);

    /// <summary>
    /// Gets the delivery instant in local time, or the empty placeholder while the carrier has not reported it.
    /// </summary>
    public string ReceivedDatetimeDisplay => FormatOptionalLocal(_parcel.ReceivedDatetimeUtc);

    /// <summary>
    /// Gets the last status report instant in local time, or the empty placeholder while no report exists.
    /// </summary>
    public string LastCheckedDatetimeDisplay => FormatOptionalLocal(_parcel.LastCheckedDatetimeUtc);

    /// <summary>
    /// Gets the 1C export instant formatted in UTC, or the empty placeholder while the parcel was not exported.
    /// </summary>
    public string MigratedTo1CDatetimeDisplay => FormatOptionalUtc(_parcel.MigratedTo1CDatetimeUtc);

    /// <summary>
    /// Gets the localized status label, or the empty placeholder while no status was observed. The draft row
    /// has no status either, so it shows the same placeholder as an unsaved value.
    /// </summary>
    public string StatusLabel => _parcel.LastStatus is { } status
        ? status.ToDisplayLabel()
        : EmptyValueDisplay;

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
    /// Validates that a tracking number is not already used by another parcel.
    /// </summary>
    /// <param name="value">The candidate tracking number.</param>
    /// <param name="context">The validation context, used to reach the owning row.</param>
    /// <returns>The validation outcome.</returns>
    public static ValidationResult? ValidatePaymentNumberUniqueness(object? value, ValidationContext context)
    {
        if (context.ObjectInstance is not ParcelRowViewModel row)
        {
            return ValidationResult.Success;
        }

        var paymentNumber = value as string;
        if (TextField.IsEmpty(paymentNumber))
        {
            // The Required attribute owns the "missing value" case.
            return ValidationResult.Success;
        }

        long? excludingId = row.IsDraft ? null : row.Id;
        return row._isPaymentNumberAvailable(paymentNumber, excludingId)
            ? ValidationResult.Success
            : new ValidationResult("Такой номер счёта уже добавлен");
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
    /// Copies the edited values back onto the backing parcel after a successful save, so the row and its
    /// stored snapshot agree once more.
    /// </summary>
    /// <param name="paymentNumber">The persisted payment number.</param>
    /// <param name="trackId">The persisted tracking number.</param>
    /// <param name="comment">The persisted comment, or <see langword="null"/>.</param>
    public void ApplyPersistedValues(string paymentNumber, string trackId, string? comment)
    {
        PaymentNumber = paymentNumber;
        TrackId = trackId;
        Comment = comment ?? string.Empty;
        _parcel.PaymentNumber = paymentNumber;
        _parcel.TrackId = trackId;
        _parcel.Comment = comment;
    }

    /// <summary>
    /// Restores the values of the last successful save after a rejected edit, so a row that could not be
    /// persisted does not keep the rejected values on screen.
    /// </summary>
    public void RevertToStoredValues()
    {
        PaymentNumber = _parcel.PaymentNumber;
        TrackId = _parcel.TrackId;
        Comment = _parcel.Comment ?? string.Empty;
        ValidateAllProperties();
    }

    /// <summary>
    /// Applies a freshly polled tracking result to this row so the grid reflects it without a full reload.
    /// </summary>
    /// <param name="result">The result the provider returned for this parcel.</param>
    public void ApplyTrackingResult(TrackingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        _parcel.LastStatus = result.CurrentStatus;
        _parcel.LastCheckedDatetimeUtc = result.StatusDatetimeUtc;

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(HasLastStatus));
        OnPropertyChanged(nameof(CanCheckStatus));
        OnPropertyChanged(nameof(LastCheckedDatetimeUtc));
        OnPropertyChanged(nameof(LastCheckedDatetimeDisplay));
    }

    /// <summary>
    /// Gets the backing parcel so the owning view model can hand it to the service layer.
    /// </summary>
    /// <returns>The backing parcel.</returns>
    public Parcel GetParcel() => _parcel;

    /// <summary>
    /// Resolves a provider code to its display name, falling back to the code itself for a provider that
    /// is no longer registered.
    /// </summary>
    /// <param name="code">The provider code to resolve.</param>
    /// <returns>The display name shown to the operator.</returns>
    private string ResolveTrackingServiceName(string code) =>
        TrackingServices
            .FirstOrDefault(descriptor => string.Equals(descriptor.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.DisplayName
        ?? code;

    /// <summary>
    /// Formats a mandatory instant in local time.
    /// </summary>
    /// <param name="utc">The instant to format.</param>
    /// <returns>The formatted text.</returns>
    private string FormatLocal(DateTime utc) =>
        _localTimeZone.ToLocal(utc).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats an optional instant in local time, or the empty-value placeholder.
    /// </summary>
    /// <param name="utc">The instant to format.</param>
    /// <returns>The formatted text.</returns>
    private string FormatOptionalLocal(DateTime? utc) =>
        utc is null ? EmptyValueDisplay : FormatLocal(utc.Value);

    /// <summary>
    /// Formats an optional UTC instant with an explicit UTC marker, or the empty-value placeholder.
    /// </summary>
    /// <param name="utc">The instant to format.</param>
    /// <returns>The formatted text.</returns>
    private static string FormatOptionalUtc(DateTime? utc) =>
        utc is null
            ? EmptyValueDisplay
            : $"{utc.Value.ToString(TimestampFormat, CultureInfo.InvariantCulture)} UTC";

    /// <summary>
    /// Notifies the computed cell content when the payment number changes.
    /// </summary>
    /// <param name="value">The new payment number.</param>
    partial void OnPaymentNumberChanged(string value) => OnPropertyChanged(nameof(PaymentNumberCell));

    /// <summary>
    /// Notifies the computed cell content and the tracking link when the tracking number changes.
    /// </summary>
    /// <param name="value">The new tracking number.</param>
    partial void OnTrackIdChanged(string value)
    {
        OnPropertyChanged(nameof(TrackIdCell));
        OnPropertyChanged(nameof(TrackingUrl));
    }

    /// <summary>
    /// Notifies the computed cell content when the comment changes.
    /// </summary>
    /// <param name="value">The new comment.</param>
    partial void OnCommentChanged(string value) => OnPropertyChanged(nameof(CommentCell));

    /// <summary>
    /// Notifies the computed display members when the selected provider changes.
    /// </summary>
    /// <param name="value">The new provider code.</param>
    partial void OnSelectedTrackingServiceCodeChanged(string value)
    {
        OnPropertyChanged(nameof(TrackingServiceDisplay));
        OnPropertyChanged(nameof(IsTrackingServiceMissing));
    }

    /// <summary>
    /// Notifies the computed display member when the row number changes.
    /// </summary>
    /// <param name="value">The new row number.</param>
    partial void OnRowNumberChanged(int value) => OnPropertyChanged(nameof(RowNumberDisplay));
}
