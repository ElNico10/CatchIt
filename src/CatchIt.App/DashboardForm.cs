using CatchIt.Core;
using CatchIt.Core.Search;

namespace CatchIt.App;

/// <summary>
/// The dashboard (user story #15): the watched products whose price has reached their target, so the user finds
/// out by opening the app rather than through email or push notifications. A second list shows the products still
/// above their target, nearest first, and every row shows how the price compares with the product's usual price.
/// </summary>
/// <remarks>
/// Only logged-in users get this far. The lists are built once, when the window opens, from the current
/// (SIMULATED) prices. Double-clicking a row opens that product's detail window.
/// </remarks>
public partial class DashboardForm : Form
{
    private readonly ProductDetailService _details;
    private readonly WatchlistService _watchlist;
    private readonly DashboardService _dashboard;

    /// <summary>One grid row: the text shown, plus the listing a double-click opens.</summary>
    private sealed record Row(long? ListingId, string Product, string Brand, string Price, string Target, string Gap, string Average);

    /// <summary>Creates the dashboard.</summary>
    /// <param name="dashboard">Supplies the two lists.</param>
    /// <param name="details">Supplies the detail window opened by double-clicking a row.</param>
    /// <param name="watchlist">Passed on to the detail window for its watchlist button.</param>
    public DashboardForm(DashboardService dashboard, ProductDetailService details, WatchlistService watchlist)
    {
        _dashboard = dashboard;
        _details = details;
        _watchlist = watchlist;
        InitializeComponent();
        lblDisclaimer.Text = Disclaimer.SimulatedData;
        Reload();
    }

    /// <summary>Rebuilds both lists. Also run after a detail window closes, since the user may have changed the watchlist there.</summary>
    private void Reload()
    {
        var view = _dashboard.Load();
        Fill(dgvBelow, lblBelowEmpty, view.BelowTarget, below: true, "Nothing has reached its target price yet.");
        Fill(dgvNotYet, lblNotYetEmpty, view.NotYetAtTarget, below: false, "No other watched product has a target price.");

        var total = view.BelowTarget.Count + view.NotYetAtTarget.Count + view.WithoutTarget;
        lblNote.Text = total == 0
            ? "Your watchlist is empty. Add products from a product's detail window."
            : view.WithoutTarget > 0
                ? $"{view.WithoutTarget} watched product{(view.WithoutTarget == 1 ? " has" : "s have")} no target price, so can't be checked. Set one in My watchlist."
                : "";
    }

    /// <summary>A grid selects its first row when the window first appears, so clear it then: no row looks chosen until the user picks one.</summary>
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        foreach (var grid in new[] { dgvBelow, dgvNotYet })
        {
            grid.CurrentCell = null;
            grid.ClearSelection();
        }
    }

    private void Fill(DataGridView grid, Label empty, IReadOnlyList<DashboardEntry> entries, bool below, string emptyText)
    {
        grid.DataSource = entries.Select(e => ToRow(e, below)).ToList();
        grid.Visible = entries.Count > 0;
        empty.Text = emptyText;
        empty.Visible = entries.Count == 0;
    }

    private static Row ToRow(DashboardEntry e, bool below)
    {
        var item = e.Item;
        return new Row(
            item.ListingId, item.ProductName, item.BrandName,
            item.CurrentPrice is { } price ? Display.Money(price) : "No price",
            item.TargetPrice is { } target ? Display.Money(target) : "",
            GapText(e, below), AverageText(e));
    }

    /// <summary>"$5.00 under (10%)" for a product at its target, "$5.00 to go (10%)" for one still above it.</summary>
    private static string GapText(DashboardEntry e, bool below) =>
        e.GapToTarget is { } gap && e.GapToTargetFraction is { } fraction
            ? $"{Display.Money(Math.Abs(gap))} {(below ? "under" : "to go")} ({Math.Abs(fraction):P0})"
            : "";

    /// <summary>How the price compares with the usual price: "12% below", "3% above" or "about the same".</summary>
    private static string AverageText(DashboardEntry e) =>
        e.VersusAverageFraction switch
        {
            null => "Not enough history",
            { } f when Math.Abs(f) < 0.005 => "About the same",
            { } f when f < 0 => $"{-f:P0} below",
            { } f => $"{f:P0} above",
        };

    /// <summary>A double-click on a row opens the product's detail window.</summary>
    private void grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || sender is not DataGridView grid || grid.Rows[e.RowIndex].DataBoundItem is not Row { ListingId: { } listing })
            return;

        using (var window = new ProductDetailForm(_details, _watchlist, listing))
            window.ShowDialog(this);
        Reload();
    }
}
