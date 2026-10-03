using MailIntegrator.Models;

namespace MailIntegrator.Services;

/// <summary>
/// The carrier independent answer to a tracking request.
/// </summary>
/// <param name="TrackingId">The tracking number the provider answered for.</param>
/// <param name="CurrentStatus">The generic status adapted from the provider vocabulary.</param>
/// <param name="StatusDatetimeUtc">The instant the reported status belongs to, in UTC.</param>
public sealed record TrackingResult(string TrackingId, ParcelStatus CurrentStatus, DateTime StatusDatetimeUtc);
