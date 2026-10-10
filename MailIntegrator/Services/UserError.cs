namespace MailIntegrator.Services;

/// <summary>
/// One handled failure that the operator should see, for example a failed tracking call.
/// </summary>
/// <remarks>
/// <para>
/// The reason is always a short, user-facing Russian sentence that contains no secret material and no raw
/// provider payload; the provider name and the tracking number are added by whoever reports the error, so a
/// concrete provider never has to build the message itself.
/// </para>
/// <para>
/// <see cref="ToString"/> returns <see cref="DisplayText"/> so a footer can bind a heterogeneous
/// collection of messages with a plain <c>{Binding}</c>.
/// </para>
/// </remarks>
/// <param name="ProviderName">The display name of the provider, or its code when no display name exists.</param>
/// <param name="TrackId">The tracking number the failure belongs to, or <see langword="null"/> for a
/// provider-level failure.</param>
/// <param name="Reason">The concise Russian reason for the failure.</param>
public sealed record UserError(string ProviderName, string? TrackId, string Reason)
{
    /// <summary>
    /// Gets the single line shown in the footer.
    /// </summary>
    public string DisplayText => string.IsNullOrWhiteSpace(TrackId)
        ? $"{ProviderName}: {Reason}"
        : $"{ProviderName}, трек-номер {TrackId}: {Reason}";

    /// <inheritdoc />
    public override string ToString() => DisplayText;
}
