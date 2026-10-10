using MailIntegrator.Services;
using MailIntegrator.Tests.TestSupport;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the registry-driven credential dialog.
/// </summary>
public sealed class SettingsViewModelTests
{
    [Fact]
    public void Services_contains_one_editor_per_provider_plus_the_1c_target()
    {
        var (viewModel, _) = CreateSut();

        Assert.Equal(
            ["Почта России", "DHL", "DPD", "1C ERP"],
            viewModel.Services.Select(service => service.DisplayName));
    }

    [Fact]
    public void Save_requires_both_parts_of_every_credential()
    {
        var (viewModel, store) = CreateSut();
        viewModel.Services[0].Login = "user";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(0, store.Count);
        Assert.NotNull(viewModel.StatusMessage);
        Assert.Contains("Почта России", viewModel.StatusMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_writes_every_complete_credential_to_the_store()
    {
        var (viewModel, store) = CreateSut();
        viewModel.Services[0].Login = "  user-pochta  ";
        viewModel.Services[0].Password = "secret";
        viewModel.Services[3].Login = "user-1c";
        viewModel.Services[3].Password = "secret-1c";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(2, store.Count);
        Assert.Equal("user-pochta", store.GetCredential(viewModel.Services[0].Target)!.Login);
        Assert.Equal("secret", store.GetCredential(viewModel.Services[0].Target)!.Password);
        Assert.True(store.IsConfigured(CredentialTargets.OneC));
    }

    [Fact]
    public void ClearService_removes_the_credential_and_reports_it()
    {
        var (viewModel, store) = CreateSut();
        var target = viewModel.Services[1].Target;
        store.SaveCredential(target, "user-dhl", "secret");
        viewModel.Services[1].Reload();

        viewModel.ClearServiceCommand.Execute(viewModel.Services[1]);

        Assert.False(store.IsConfigured(target));
        Assert.Contains("DHL", viewModel.StatusMessage!, StringComparison.Ordinal);
    }

    /// <summary>Builds the view model under test together with the in-memory vault.</summary>
    private static (SettingsViewModel ViewModel, FakeSecretStore Store) CreateSut()
    {
        var registry = new TrackingServiceRegistry(DummyTrackingServices.CreateAll());
        var store = new FakeSecretStore();
        return (new SettingsViewModel(registry, store), store);
    }
}
