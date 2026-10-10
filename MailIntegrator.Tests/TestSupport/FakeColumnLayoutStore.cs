using MailIntegrator.Services;

namespace MailIntegrator.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IColumnLayoutStore"/> so layout tests do not need a database.
/// </summary>
public sealed class FakeColumnLayoutStore : IColumnLayoutStore
{
    /// <summary>
    /// Gets or sets the keys a load returns; <see langword="null"/> means "never chosen".
    /// </summary>
    public IReadOnlyList<string>? VisibleColumnKeys { get; set; }

    /// <summary>
    /// Gets the number of times the visible set was written.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Gets the keys of the last write.
    /// </summary>
    public IReadOnlyList<string>? LastSavedKeys { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<string>? LoadVisibleColumnKeys() => VisibleColumnKeys;

    /// <inheritdoc />
    public void SaveVisibleColumnKeys(IReadOnlyCollection<string> keys)
    {
        SaveCount++;
        LastSavedKeys = keys.ToList();
        VisibleColumnKeys = LastSavedKeys;
    }
}
