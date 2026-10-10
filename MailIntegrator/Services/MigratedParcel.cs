namespace MailIntegrator.Services;

/// <summary>
/// A parcel record that already exists in the 1C target system.
/// </summary>
/// <param name="PaymentNumber">
/// The payment number found in the target system. It is the target's primary key and identifies exactly
/// one local parcel.
/// </param>
/// <param name="CreatedDatetimeUtc">The instant the target system recorded the parcel, in UTC.</param>
public sealed record MigratedParcel(string PaymentNumber, DateTime CreatedDatetimeUtc);
