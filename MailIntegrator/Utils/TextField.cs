using System.Diagnostics.CodeAnalysis;

namespace MailIntegrator.Utils;

/// <summary>
/// Reusable presence and normalisation helpers for the free-text fields the operator edits.
/// </summary>
/// <remarks>
/// Centralised so every field treats "blank" the same way and no caller has to repeat
/// <see cref="string.IsNullOrWhiteSpace(string)"/> or the null-to-empty-string trim dance.
/// </remarks>
public static class TextField
{
    /// <summary>
    /// Reports whether a text field carries no usable value, treating <see langword="null"/>,
    /// an empty string and whitespace alike.
    /// </summary>
    /// <param name="value">The raw field value.</param>
    /// <returns><see langword="true"/> when the field has no usable value.</returns>
    public static bool IsEmpty([NotNullWhen(false)] string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Trims a text field and turns <see langword="null"/> into an empty string.
    /// </summary>
    /// <param name="value">The raw field value.</param>
    /// <returns>The trimmed value, never <see langword="null"/>.</returns>
    public static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
