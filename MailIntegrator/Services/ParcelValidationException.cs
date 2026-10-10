namespace MailIntegrator.Services;

/// <summary>
/// Raised when an operation would violate a parcel business rule.
/// </summary>
/// <remarks>
/// Messages are technical diagnostics. User facing text is produced by the view models so that the
/// service layer stays free of presentation concerns.
/// </remarks>
public sealed class ParcelValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParcelValidationException"/> class.
    /// </summary>
    /// <param name="message">The technical description of the rule violation.</param>
    public ParcelValidationException(string message)
        : base(message)
    {
    }
}
