using System.Globalization;
using CatchIt.Core;
using CatchIt.Core.Search;

namespace CatchIt.App;

/// <summary>
/// The watchlist screen (user stories #13 and #14): the products the user saved, each with its lowest current
/// price and optional target price, flagged when the price has reached the target. Products can be removed here,
/// and double-clicking one opens its detail window.
/// </summary>
/// <remarks>
/// Only logged-in users get this far (the main window checks access first). Prices are SIMULATED, and the window says so.
/// </remarks>
public partial class WatchlistForm : Form
{
    private readonly WatchlistService _watchlist;
    private readonly ProductDetailService _details;

    /// <summary>One grid row: the displayed text for a watched product, plus the ids the buttons need.</summary>
    private sealed record WatchRow(
        long ProductId, long? ListingId, string Product, string Brand, string Price, string Marketplace,
        string Target, string Status, double? TargetValue);

    /// <summary>Creates the watchlist screen.</summary>
    /// <param name="watchlist">Supplies and changes the user's watchlist.</param>
    /// <param name="details">Supplies the detail window opened by double-clicking a product.</param>
    public WatchlistForm(WatchlistService watchlist, ProductDetailService details)
    {
        _watchlist = watchlist;
        _details = details;
        InitializeComponent();
        lblDisclaimer.Text = Disclaimer.SimulatedData;
        LoadList();
    }

    private WatchRow? Selected =>
        dgvWatch.CurrentRow?.DataBoundItem as WatchRow;

    /// <summary>Reloads the grid, keeping the same product selected when it is still there.</summary>
    private void LoadList()
    {
        var keep = Selected?.ProductId;
        var rows = _watchlist.List().Select(w => new WatchRow(
            w.ProductId, w.ListingId, w.ProductName, w.BrandName,
            w.CurrentPrice is { } p ? Display.Money(p) : "No price",
            w.MarketplaceName ?? "",
            w.TargetPrice is { } t ? Display.Money(t) : "",
            w.IsBelowTarget ? "Below target" : "",
            w.TargetPrice)).ToList();

        dgvWatch.DataSource = rows;
        lblEmpty.Visible = rows.Count == 0;
        dgvWatch.Visible = rows.Count > 0;
        var at = rows.FindIndex(r => r.ProductId == keep);
        if (at >= 0) dgvWatch.CurrentCell = dgvWatch.Rows[at].Cells[0];
        ShowSelection();
    }

    /// <summary>Enables the buttons only when a product is selected and shows its target in the box.</summary>
    private void ShowSelection()
    {
        var row = Selected;
        txtTarget.Enabled = btnSetTarget.Enabled = btnClearTarget.Enabled = btnRemove.Enabled = row != null;
        txtTarget.Text = row?.TargetValue is { } t ? t.ToString("0.00", CultureInfo.CurrentCulture) : "";
    }

    private void Say(WatchlistResult result)
    {
        lblStatus.ForeColor = result.Success ? Color.ForestGreen : Color.Firebrick;
        lblStatus.Text = result.Message;
    }

    private void dgvWatch_SelectionChanged(object? sender, EventArgs e) => ShowSelection();

    private void btnRemove_Click(object? sender, EventArgs e)
    {
        if (Selected is not { } row) return;
        Say(_watchlist.Remove(row.ProductId));
        LoadList();
    }

    private void btnSetTarget_Click(object? sender, EventArgs e)
    {
        if (Selected is not { } row) return;

        // Accept "$25.50" as well as "25.50"; text that is not a number gets the same message as a non-positive one.
        var text = txtTarget.Text.Trim().TrimStart('$').Trim();
        if (text.Length == 0)
        {
            Say(new WatchlistResult(false, "Enter a target price, or use Clear target"));
            return;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var target))
        {
            Say(new WatchlistResult(false, WatchlistService.InvalidTargetMessage));
            return;
        }
        Say(_watchlist.SetTargetPrice(row.ProductId, target));
        LoadList();
    }

    private void btnClearTarget_Click(object? sender, EventArgs e)
    {
        if (Selected is not { } row) return;
        Say(_watchlist.SetTargetPrice(row.ProductId, null));
        LoadList();
    }

    /// <summary>Double-click opens the product's cheapest listing; the list is reloaded afterwards, since it may have changed there.</summary>
    private void dgvWatch_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgvWatch.Rows[e.RowIndex].DataBoundItem is not WatchRow row) return;
        if (row.ListingId is not { } listing)
        {
            Say(new WatchlistResult(false, "This product has no price right now, so there is nothing to show"));
            return;
        }
        using (var window = new ProductDetailForm(_details, _watchlist, listing))
            window.ShowDialog(this);
        LoadList();
    }
}
