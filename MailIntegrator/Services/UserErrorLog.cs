using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MailIntegrator.Services;

/// <summary>
/// Default <see cref="IUserErrorLog"/> implementation backed by an <see cref="ObservableCollection{T}"/>.
/// </summary>
/// <remarks>
/// The mutable collection is private and only exposed as a <see cref="ReadOnlyObservableCollection{T}"/>, so
/// callers can watch it but only change it through <see cref="BeginPass"/> and <see cref="Report"/>.
/// </remarks>
public sealed class UserErrorLog : ObservableObject, IUserErrorLog
{
    private readonly ObservableCollection<UserError> _errors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="UserErrorLog"/> class.
    /// </summary>
    public UserErrorLog() => Errors = new ReadOnlyObservableCollection<UserError>(_errors);

    /// <inheritdoc />
    public ReadOnlyObservableCollection<UserError> Errors { get; }

    /// <inheritdoc />
    public bool HasErrors => _errors.Count > 0;

    /// <inheritdoc />
    public void BeginPass()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        _errors.Clear();
        OnPropertyChanged(nameof(HasErrors));
    }

    /// <inheritdoc />
    public void Report(UserError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var wasEmpty = _errors.Count == 0;
        _errors.Add(error);

        if (wasEmpty)
        {
            OnPropertyChanged(nameof(HasErrors));
        }
    }
}
