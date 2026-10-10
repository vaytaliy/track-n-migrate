namespace MailIntegrator.Models;

/// <summary>
/// The carrier independent status of a parcel.
/// </summary>
/// <remarks>
/// Every tracking provider maps its own status vocabulary onto these members; a value that cannot be
/// mapped becomes <see cref="Unknown"/>, which the views render as <c>?</c>.
/// <see cref="Unknown"/> is the default so an absent or unmapped status never produces a misleading
/// value.
/// </remarks>
public enum ParcelStatus
{
    /// <summary>No status was reported, or the provider value could not be mapped.</summary>
    Unknown = 0,

    /// <summary>The parcel is registered but has not been handed to the carrier yet.</summary>
    Processing,

    /// <summary>The parcel is moving through the carrier network.</summary>
    InTransit,

    /// <summary>The parcel is going through customs.</summary>
    Customs,

    /// <summary>The parcel is on its final leg to the recipient.</summary>
    OutForDelivery,

    /// <summary>The parcel failed, which is a final failure state.</summary>
    Exception,

    /// <summary>The parcel was delivered, which is a final success state.</summary>
    Delivered,
}
