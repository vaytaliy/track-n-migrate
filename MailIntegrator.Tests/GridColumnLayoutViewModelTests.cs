using MailIntegrator.Tests.TestSupport;
using MailIntegrator.ViewModels;

namespace MailIntegrator.Tests;

/// <summary>
/// Covers the columns popover state: defaults, stored preferences, locked key columns and reset.
/// </summary>
public sealed class GridColumnLayoutViewModelTests
{
    [Fact]
    public void Without_a_stored_preference_the_catalog_defaults_apply()
    {
        var store = new FakeColumnLayoutStore { VisibleColumnKeys = null };

        var layout = new GridColumnLayoutViewModel(store);

        Assert.Equal(
            GridColumnCatalog.DefaultVisibleKeys.OrderBy(key => key),
            layout.AllColumns.Where(option => option.IsVisible).Select(option => option.Key).OrderBy(key => key));
        Assert.Equal(GridColumnCatalog.All.Count, layout.AllColumns.Count);
    }

    [Fact]
    public void The_popover_lists_the_business_columns_then_the_system_columns()
    {
        var layout = new GridColumnLayoutViewModel(new FakeColumnLayoutStore());

        Assert.Equal(GridColumnCatalog.General.Select(column => column.Key), layout.GeneralColumns.Select(o => o.Key));
        Assert.Equal(GridColumnCatalog.System.Select(column => column.Key), layout.SystemColumns.Select(o => o.Key));
    }

    [Fact]
    public void A_stored_subset_hides_every_column_that_is_not_in_it()
    {
        var store = new FakeColumnLayoutStore
        {
            VisibleColumnKeys = ["PaymentNumber", "TrackId", "Comment"],
        };

        var layout = new GridColumnLayoutViewModel(store);

        Assert.True(Find(layout, "PaymentNumber").IsVisible);
        Assert.True(Find(layout, "TrackId").IsVisible);
        Assert.True(Find(layout, "Comment").IsVisible);
        Assert.False(Find(layout, "Status").IsVisible);
        Assert.False(Find(layout, "CreatedDatetimeUtc").IsVisible);
    }

    [Fact]
    public void Locked_key_columns_stay_visible_even_when_the_stored_set_omits_them()
    {
        var store = new FakeColumnLayoutStore { VisibleColumnKeys = ["Comment"] };

        var layout = new GridColumnLayoutViewModel(store);

        Assert.True(Find(layout, "PaymentNumber").IsVisible);
        Assert.True(Find(layout, "TrackId").IsVisible);
        Assert.True(Find(layout, "PaymentNumber").IsLocked);
        Assert.False(Find(layout, "PaymentNumber").CanToggle);
    }

    [Fact]
    public void Unknown_stored_keys_are_ignored()
    {
        var store = new FakeColumnLayoutStore { VisibleColumnKeys = ["PaymentNumber", "TrackId", "RemovedColumn"] };

        var layout = new GridColumnLayoutViewModel(store);

        Assert.DoesNotContain(layout.AllColumns, option => option.Key == "RemovedColumn");
    }

    [Fact]
    public void Toggling_a_column_persists_the_new_set_immediately()
    {
        var store = new FakeColumnLayoutStore { VisibleColumnKeys = null };
        var layout = new GridColumnLayoutViewModel(store);
        var sentColumn = Find(layout, "SentDatetimeUtc");
        Assert.False(sentColumn.IsVisible);

        sentColumn.IsVisible = true;

        Assert.Contains("SentDatetimeUtc", store.LastSavedKeys!);
        Assert.Contains("PaymentNumber", store.LastSavedKeys!);
    }

    [Fact]
    public void Reset_restores_the_defaults_and_persists_them_once()
    {
        var store = new FakeColumnLayoutStore { VisibleColumnKeys = ["PaymentNumber", "TrackId"] };
        var layout = new GridColumnLayoutViewModel(store);
        Assert.False(Find(layout, "Status").IsVisible);
        var savesBeforeReset = store.SaveCount;

        layout.ResetToDefaultsCommand.Execute(null);

        Assert.True(Find(layout, "Status").IsVisible);
        Assert.True(Find(layout, "CreatedDatetimeUtc").IsVisible);
        Assert.False(Find(layout, "SentDatetimeUtc").IsVisible);
        Assert.Equal(savesBeforeReset + 1, store.SaveCount);
        Assert.Equal(
            GridColumnCatalog.DefaultVisibleKeys.OrderBy(key => key),
            store.LastSavedKeys!.OrderBy(key => key));
    }

    /// <summary>Finds a popover option by its column key.</summary>
    private static ColumnOptionViewModel Find(GridColumnLayoutViewModel layout, string key) =>
        layout.AllColumns.Single(option => option.Key == key);
}
