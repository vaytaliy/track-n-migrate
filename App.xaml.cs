using System.Windows;
using System.Windows.Threading;
using MailIntegrator.Data;
using MailIntegrator.Infrastructure;
using MailIntegrator.Services;
using MailIntegrator.ViewModels;
using MailIntegrator.Views;

namespace MailIntegrator;

/// <summary>
/// Application entry point and composition root: builds the object graph and shows the main window.
/// </summary>
public partial class App : Application
{
    private AppDatabase? _database;
    private MainViewModel? _mainViewModel;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var clock = new SystemClock();

        _database = new AppDatabase(AppPaths.DatabaseFile);
        _database.Initialize();

        var parcelRepository = new ParcelRepository(_database);
        var parcelService = new ParcelService(parcelRepository, clock);
        var secretStore = new CredentialManagerSecretStore();
        var syncService = new DummySyncService();
        var dialogService = new WpfDialogService(() => new SettingsViewModel(secretStore));

        _mainViewModel = new MainViewModel(parcelService, syncService, dialogService, clock);
        _mainViewModel.Initialize();

        var mainWindow = new MainWindow(_mainViewModel);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _mainViewModel?.Dispose();
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        base.OnExit(e);
    }

    /// <summary>
    /// Reports an unexpected failure without echoing any secret material, and keeps the app running.
    /// </summary>
    /// <param name="sender">The application.</param>
    /// <param name="e">The exception details.</param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Произошла непредвиденная ошибка:{Environment.NewLine}{e.Exception.Message}",
            "Ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
