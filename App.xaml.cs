using System.IO;
using System.Windows;
using System.Windows.Threading;
using MailIntegrator.Configuration;
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
        // TEMP DIAGNOSTIC: capture any reflection-invocation failure with its full stack.
        AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
        {
            if (args.Exception is not System.Reflection.TargetInvocationException
                && args.Exception.InnerException is null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.AppendAllText(
                    Path.Combine(AppPaths.DataDirectory, "firstchance.log"),
                    $"=== {DateTime.Now:O}{Environment.NewLine}{args.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (IOException)
            {
            }
        };

        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var clock = new SystemClock();
        var localTimeZone = new SystemLocalTimeZone();

        _database = new AppDatabase(AppPaths.DatabaseFile);
        _database.Initialize();

        var appConfigStore = new AppConfigStore(AppPaths.AppConfigFile);
        var statusMappings = new StatusMappingCatalog(appConfigStore.Load());

        var parcelRepository = new ParcelRepository(_database);
        var appSettingsRepository = new AppSettingsRepository(_database, clock);
        var columnLayoutStore = new ColumnLayoutStore(appSettingsRepository);
        var trackingServiceRegistry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll(statusMappings));
        var parcelService = new ParcelService(parcelRepository, clock, trackingServiceRegistry);
        var secretStore = new CredentialManagerSecretStore();

        // One log for the whole application: the passes report into it and the footer displays it.
        var errorLog = new UserErrorLog();
        var trackingSyncService = new ParcelTrackingSyncService(
            parcelService,
            trackingServiceRegistry,
            secretStore,
            errorLog);
        var migrationSyncService = new DummyMigrationSyncService();
        var dialogService = new WpfDialogService(() => new SettingsViewModel(trackingServiceRegistry, secretStore));

        _mainViewModel = new MainViewModel(
            parcelService,
            trackingSyncService,
            migrationSyncService,
            dialogService,
            clock,
            localTimeZone,
            trackingServiceRegistry,
            columnLayoutStore,
            errorLog);
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
        // The message alone is useless for a wrapped exception: a TargetInvocationException hides its
        // real cause in InnerException, so the whole chain (and its stack trace) is reported and logged.
        var report = BuildExceptionReport(e.Exception);
        LogUnhandledException(e.Exception, report);

        MessageBox.Show(
            $"Произошла непредвиденная ошибка:{Environment.NewLine}{report}",
            "Ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    /// <summary>
    /// Flattens an exception and every inner exception into one report line per level.
    /// </summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>The multi-line report shown to the operator and written to the log.</returns>
    private static string BuildExceptionReport(Exception exception)
    {
        var report = new System.Text.StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            report.Append(current.GetType().FullName).Append(": ").Append(current.Message).AppendLine();
        }

        return report.ToString();
    }

    /// <summary>
    /// Appends the full exception chain (messages, inner exceptions and stack traces) to a per-user log.
    /// </summary>
    /// <param name="exception">The exception whose full <see cref="Exception.ToString"/> is logged.</param>
    /// <param name="report">The flattened report used as the searchable entry header.</param>
    private static void LogUnhandledException(Exception exception, string report)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.AppendAllText(
                Path.Combine(AppPaths.DataDirectory, "unhandled-errors.log"),
                $"=== {DateTime.Now:O}{Environment.NewLine}{report}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never mask the original failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must never mask the original failure.
        }
    }
}
