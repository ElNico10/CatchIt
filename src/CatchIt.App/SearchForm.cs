using System.Globalization;
using CatchIt.Core.Search;

namespace CatchIt.App;

/// <summary>
/// The product search screen (user stories #5, #6, #9, #10, #11 and #18). Results appear as the user types,
/// cheapest listing first, and can be narrowed by brand quality and seller rating.
/// </summary>
/// <remarks>
/// All searching runs on a background thread through a <see cref="LiveSearchController{TPage}"/>, so
/// the window never freezes, and only the newest search's results are ever shown.
/// <list type="bullet">
/// <item><description>While typing, a quick search shows the cheapest listings among the first matching
/// products. If more products matched than it looked at, the status line says so.</description></item>
/// <item><description>Pressing Search (or Enter) runs the full search: the cheapest listings across every match.
/// Changing a filter repeats whichever kind of search was last run.</description></item>
/// <item><description>With nothing typed, the screen lists the cheapest listings in the whole catalog, so the filters
/// can be used on their own. It starts this way when opened.</description></item>
/// </list>
/// Prices and seller ratings are SIMULATED demo data, and the screen says so.
/// </remarks>
public partial class SearchForm : Form
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>One row of the results grid. The property names match the grid columns' data property names.</summary>
    private sealed record ResultRow(string Product, string Brand, string Quality, string Marketplace, string SellerRating, string Price);

    private readonly LiveSearchController<ListingSearchPage> _controller;

    // The filters currently chosen. The background search reads this when it runs, so it holds an immutable
    // record that the screen replaces as a whole (never a control, which must only be touched on this thread).
    private volatile ListingFilters _filters = ListingFilters.None;

    // True once the user has pressed Search, so that changing a filter repeats a full search, not a quick one.
    private bool _lastWasFullSearch;

    // Set while the code itself changes the filter boxes, so that does not trigger a search per box.
    private bool _settingFilters;

    /// <summary>Creates the search screen.</summary>
    /// <param name="search">The service that finds the listings.</param>
    public SearchForm(ListingSearchService search)
    {
        InitializeComponent();

        // Created after the controls exist, on this (the UI) thread, so the controller delivers its
        // results here and the handlers below can update controls directly.
        _controller = LiveSearchController.For(search, () => _filters);
        _controller.ResultsReady += OnResultsReady;
        _controller.SearchFailed += OnSearchFailed;
        FormClosed += (_, _) => _controller.Dispose();

        _settingFilters = true;
        cmbTier.SelectedIndex = 0;
        cmbRating.SelectedIndex = 0;
        _settingFilters = false;

        lblStatus.ForeColor = SystemColors.GrayText;
        lblStatus.Text = "Loading the catalog...";

        // With nothing typed the screen browses the whole catalog, so start by listing it.
        Shown += (_, _) => _controller.Submit(string.Empty);
    }

    // ---- What the user does ----

    /// <summary>Every keystroke: a quick, debounced search.</summary>
    private void txtSearch_TextChanged(object? sender, EventArgs e)
    {
        _lastWasFullSearch = false;
        _controller.TextChanged(txtSearch.Text);
    }

    /// <summary>The Search button (and Enter): the full, exact search.</summary>
    private void btnSearch_Click(object? sender, EventArgs e)
    {
        _lastWasFullSearch = true;
        lblStatus.Text = "Searching...";
        _controller.Submit(txtSearch.Text);
    }

    /// <summary>A filter box changed: remember the new filters and repeat the last kind of search.</summary>
    private void filter_Changed(object? sender, EventArgs e)
    {
        if (_settingFilters) return;
        _filters = new ListingFilters(SelectedTier(), SelectedMinRating());
        Rerun();
    }

    private void btnClearFilters_Click(object? sender, EventArgs e)
    {
        _settingFilters = true;
        cmbTier.SelectedIndex = 0;
        cmbRating.SelectedIndex = 0;
        _settingFilters = false;

        _filters = ListingFilters.None;
        Rerun();
    }

    private void Rerun()
    {
        if (_lastWasFullSearch) _controller.Submit(txtSearch.Text);
        else _controller.TextChanged(txtSearch.Text);
    }

    /// <summary>The tier chosen in the quality box: 1 Budget, 2 Standard, 3 Premium, or null for any.</summary>
    private int? SelectedTier() => cmbTier.SelectedIndex > 0 ? cmbTier.SelectedIndex : null;

    /// <summary>The minimum seller rating chosen, or null for any.</summary>
    private double? SelectedMinRating() => cmbRating.SelectedIndex switch
    {
        1 => 3.0,
        2 => 3.5,
        3 => 4.0,
        4 => 4.5,
        _ => null,
    };

    // ---- What the screen shows ----

    private void OnResultsReady(object? sender, LiveSearchResult<ListingSearchPage> result)
    {
        var page = result.Page;
        dgvResults.DataSource = page.Hits.Select(ToRow).ToList();
        dgvResults.ClearSelection(); // nothing happens on click yet, so do not highlight a row as if it were chosen
        lblStatus.ForeColor = SystemColors.ControlText;

        // Text with no searchable words (empty, spaces or punctuation) means the whole catalog is being browsed.
        lblStatus.Text = StatusFor(page, browsing: ProductSearchService.BuildQuery(result.Text) == null);
    }

    private void OnSearchFailed(object? sender, Exception error)
    {
        lblStatus.ForeColor = Color.Firebrick;
        lblStatus.Text = $"The search could not be completed: {error.Message}";
    }

    /// <summary>The line above the grid: how many results, or why there are none (stories #5 and #10).</summary>
    /// <param name="page">The results being shown.</param>
    /// <param name="browsing">True when no search text was typed, so these are the cheapest listings in the whole catalog.</param>
    private string StatusFor(ListingSearchPage page, bool browsing)
    {
        var shown = page.Hits.Count;

        if (shown == 0)
        {
            if (!page.TextMatchedAnyProduct) return "No products found";
            return _filters.IsActive ? "No products match your filters" : "No listings are available for this search";
        }

        // Browsing: say what is being listed, and that typing or filtering narrows it.
        if (browsing)
        {
            var scope = _filters.IsActive ? "matching your filters" : "in the catalog";
            return page.TotalListings is { } all
                ? $"{Plural(all, "listing")} {scope}, lowest price first."
                : $"Showing the {shown:N0} cheapest listings {scope}. Type to search, or use the filters to narrow them.";
        }

        // A full search: say how many of all the matching listings are shown.
        if (page.TotalListings is { } total)
            return total > shown
                ? $"Showing the {shown:N0} cheapest of {total:N0} listings, lowest price first."
                : $"{Plural(shown, "listing")}, lowest price first.";

        // A quick search that covered every matching product is as good as the full one.
        if (page.IsComplete)
            return $"{Plural(shown, "listing")} from {Plural(page.TotalProducts ?? 0, "product")}, lowest price first.";

        // A quick search that only looked at the first matches: be honest about it.
        var products = $"{page.TotalProducts:N0}{(page.TotalsAreCapped ? "+" : "")}";
        return $"Quick results from the first matches ({products} products match). Press Search to see the cheapest listings across all of them.";
    }

    private static string Plural(long count, string noun) => $"{count:N0} {noun}{(count == 1 ? "" : "s")}";

    private static ResultRow ToRow(ListingHit hit) => new(
        hit.ProductName,
        hit.BrandName,
        TierName(hit.QualityTier),
        hit.MarketplaceName,
        hit.SellerRating.ToString("0.0", Invariant),
        // A listing with no price says so, rather than showing a misleading number (story #6).
        hit.CurrentPrice is { } price ? "$" + price.ToString("N2", Invariant) : "Price unavailable");

    private static string TierName(int tier) => tier switch
    {
        1 => "Budget",
        2 => "Standard",
        3 => "Premium",
        _ => tier.ToString(Invariant),
    };
}
