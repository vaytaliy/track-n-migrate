namespace MailIntegrator.Services;

/// <summary>
/// A parcel record that already exists in the 1C target system.
/// </summary>
/// <param name="TrackId">The tracking number found in the target system.</param>
/// <param name="CreatedDatetimeUtc">The instant the target system recorded the parcel, in UTC.</param>
public sealed record MigratedParcel(string TrackId, DateTime CreatedDatetimeUtc);
