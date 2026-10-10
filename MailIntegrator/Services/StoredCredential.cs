namespace MailIntegrator.Services;

/// <summary>
/// A credential pair retrieved from the operating system vault.
/// </summary>
/// <param name="Login">The user name component.</param>
/// <param name="Password">The secret component.</param>
public sealed record StoredCredential(string Login, string Password);
